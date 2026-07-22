using System.Text;
using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Email;

namespace YemeniBreeze.Api.Features.Registrations;

public record RegisterRequest(string FullName, string Email, string? Phone, int GuestsCount, string? Note, string? Language);
public record RegistrationResultDto(int Id, RegistrationStatus Status, string EventSlug);
public record RegistrationDto(
    int Id, int EventId, string FullName, string Email, string? Phone,
    int GuestsCount, string? Note, RegistrationStatus Status, DateTime CreatedAt,
    DateTime? CheckedInAt);
public record CheckinRequest(string TicketCode);

public static class RegistrationsEndpoints
{
    private static RegistrationDto ToDto(Registration r) => new(
        r.Id, r.EventId, r.FullName, r.Email, r.Phone,
        r.GuestsCount, r.Note, r.Status, r.CreatedAt, r.CheckedInAt);

    public static void MapRegistrationsEndpoints(this IEndpointRouteBuilder app)
    {
        // Public: register for an event (auto-waitlist when capacity is reached)
        app.MapPost("/api/events/{slug}/register",
            async (string slug, RegisterRequest request, AppDbContext db, EmailService emails) =>
        {
            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest(new { message = "Name and email are required." });
            if (request.GuestsCount is < 1 or > 10)
                return Results.BadRequest(new { message = "Guests count must be between 1 and 10." });

            var ev = await db.Events.Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.Slug == slug && e.Status == EventStatus.Published);
            if (ev is null) return Results.NotFound();
            if (!ev.IsRegistrationOpen)
                return Results.BadRequest(new { message = "Registration for this event is closed." });

            var email = request.Email.Trim().ToLowerInvariant();
            if (ev.Registrations.Any(r => r.Email == email && r.Status != RegistrationStatus.Cancelled))
                return Results.Conflict(new { message = "This email is already registered for this event." });

            var confirmedSeats = ev.Registrations
                .Where(r => r.Status == RegistrationStatus.Confirmed)
                .Sum(r => r.GuestsCount);
            var status = confirmedSeats + request.GuestsCount <= ev.Capacity
                ? RegistrationStatus.Confirmed
                : RegistrationStatus.Waitlisted;

            var registration = new Registration
            {
                EventId = ev.Id,
                FullName = request.FullName.Trim(),
                Email = email,
                Phone = request.Phone?.Trim(),
                GuestsCount = request.GuestsCount,
                Note = request.Note?.Trim(),
                Status = status,
                Language = request.Language is "nl" or "ar" ? request.Language : "en"
            };
            db.Registrations.Add(registration);
            await db.SaveChangesAsync();

            _ = emails.SendRegistrationEmailAsync(registration, ev);

            return Results.Ok(new RegistrationResultDto(registration.Id, registration.Status, ev.Slug));
        });

        var admin = app.MapGroup("/api/admin/events/{eventId:int}/registrations").RequireAuthorization();

        admin.MapGet("/", async (int eventId, AppDbContext db) =>
            (await db.Registrations
                .Where(r => r.EventId == eventId)
                .OrderBy(r => r.Status).ThenBy(r => r.CreatedAt)
                .ToListAsync())
            .Select(ToDto));

        admin.MapPost("/{id:int}/status",
            async (int eventId, int id, StatusChange change, AppDbContext db, EmailService emails) =>
        {
            var ev = await db.Events.Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.Id == eventId);
            var registration = ev?.Registrations.FirstOrDefault(r => r.Id == id);
            if (ev is null || registration is null) return Results.NotFound();

            var previous = registration.Status;
            registration.Status = change.Status;

            // Manual promotion from the waitlist → send the ticket
            if (previous != RegistrationStatus.Confirmed && change.Status == RegistrationStatus.Confirmed)
                _ = emails.SendRegistrationEmailAsync(registration, ev, promoted: previous == RegistrationStatus.Waitlisted);

            // Cancelling a confirmed registration frees seats → auto-promote waitlisted people who fit
            var promotedRegs = previous == RegistrationStatus.Confirmed && change.Status == RegistrationStatus.Cancelled
                ? PromoteWaitlisted(ev)
                : [];

            await db.SaveChangesAsync();

            foreach (var promotedReg in promotedRegs)
                _ = emails.SendRegistrationEmailAsync(promotedReg, ev, promoted: true);

            return Results.Ok(ToDto(registration));
        });

        // Check-in by QR ticket code (door scenario)
        app.MapPost("/api/admin/events/{eventId:int}/checkin",
            async (int eventId, CheckinRequest request, AppDbContext db) =>
        {
            if (!Guid.TryParse(request.TicketCode?.Trim(), out var ticket))
                return Results.BadRequest(new { message = "Invalid ticket code." });

            var registration = await db.Registrations
                .FirstOrDefaultAsync(r => r.EventId == eventId && r.TicketCode == ticket);
            if (registration is null)
                return Results.NotFound(new { message = "Ticket not found for this event." });
            if (registration.Status != RegistrationStatus.Confirmed)
                return Results.BadRequest(new { message = $"Registration is {registration.Status}, not Confirmed.", registration = ToDto(registration) });

            var alreadyCheckedIn = registration.CheckedInAt is not null;
            if (!alreadyCheckedIn)
            {
                registration.CheckedInAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            return Results.Ok(new { alreadyCheckedIn, registration = ToDto(registration) });
        }).RequireAuthorization();

        // Manual check-in from the search fallback (no ticket needed)
        admin.MapPost("/{id:int}/checkin", async (int eventId, int id, AppDbContext db) =>
        {
            var registration = await db.Registrations
                .FirstOrDefaultAsync(r => r.EventId == eventId && r.Id == id);
            if (registration is null) return Results.NotFound();
            if (registration.Status != RegistrationStatus.Confirmed)
                return Results.BadRequest(new { message = $"Registration is {registration.Status}, not Confirmed." });

            var alreadyCheckedIn = registration.CheckedInAt is not null;
            if (!alreadyCheckedIn)
            {
                registration.CheckedInAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return Results.Ok(new { alreadyCheckedIn, registration = ToDto(registration) });
        });

        admin.MapGet("/export.csv", async (int eventId, AppDbContext db) =>
        {
            var rows = await db.Registrations
                .Where(r => r.EventId == eventId)
                .OrderBy(r => r.Status).ThenBy(r => r.CreatedAt)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("FullName,Email,Phone,Guests,Status,CheckedIn,Note,RegisteredAt");
            foreach (var r in rows)
                sb.AppendLine(string.Join(',',
                    Csv(r.FullName), Csv(r.Email), Csv(r.Phone ?? ""), r.GuestsCount,
                    r.Status, r.CheckedInAt?.ToString("yyyy-MM-dd HH:mm") ?? "",
                    Csv(r.Note ?? ""), r.CreatedAt.ToString("yyyy-MM-dd HH:mm")));

            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
                "text/csv", $"registrations-event-{eventId}.csv");
        });
    }

    /// <summary>Promotes earliest-registered waitlisted people while they fit in the remaining capacity.</summary>
    private static List<Registration> PromoteWaitlisted(Event ev)
    {
        var promoted = new List<Registration>();
        while (true)
        {
            var confirmedSeats = ev.Registrations
                .Where(r => r.Status == RegistrationStatus.Confirmed)
                .Sum(r => r.GuestsCount);
            var next = ev.Registrations
                .Where(r => r.Status == RegistrationStatus.Waitlisted
                            && confirmedSeats + r.GuestsCount <= ev.Capacity)
                .OrderBy(r => r.CreatedAt)
                .FirstOrDefault();
            if (next is null) return promoted;
            next.Status = RegistrationStatus.Confirmed;
            promoted.Add(next);
        }
    }

    public record StatusChange(RegistrationStatus Status);

    private static string Csv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
}

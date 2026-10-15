using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Email;
using YemeniBreeze.Api.Features.Security;

namespace YemeniBreeze.Api.Features.Registrations;

/// <param name="Website">Honeypot: hidden from people, so only bots fill it in.</param>
public record RegisterRequest(
    string FullName, string Email, string? Phone, int GuestsCount, string? Note, string? Language,
    string? Website = null);
public record RegistrationResultDto(int Id, RegistrationStatus Status, string EventSlug);
public record RegistrationDto(
    int Id, int EventId, string FullName, string Email, string? Phone,
    int GuestsCount, string? Note, RegistrationStatus Status, DateTime CreatedAt,
    DateTime? CheckedInAt,
    EmailKind? LastEmailKind = null, EmailStatus? LastEmailStatus = null,
    DateTime? LastEmailAt = null, string? LastEmailError = null);
public record CheckinRequest(string TicketCode);

/// <summary>What the "manage my registration" page shows. Reached with the secret token from the email.</summary>
public record ManagedRegistrationDto(
    string FullName, int GuestsCount, RegistrationStatus Status, bool CanCancel, ManagedEventDto Event);
public record ManagedEventDto(
    string Slug, string TitleEn, string TitleNl, string TitleAr,
    DateOnly Date, string StartTime, string EndTime, string Location);

public static class RegistrationsEndpoints
{
    private static RegistrationDto ToDto(Registration r, OutgoingEmail? lastEmail = null) => new(
        r.Id, r.EventId, r.FullName, r.Email, r.Phone,
        r.GuestsCount, r.Note, r.Status, r.CreatedAt, r.CheckedInAt,
        lastEmail?.Kind, lastEmail?.Status, lastEmail?.SentAt ?? lastEmail?.CreatedAt, lastEmail?.LastError);

    public static void MapRegistrationsEndpoints(this IEndpointRouteBuilder app)
    {
        // Public: register for an event (auto-waitlist when capacity is reached)
        app.MapPost("/api/events/{slug}/register",
            async (string slug, RegisterRequest request, AppDbContext db, EmailSignal emailSignal) =>
        {
            // Bots fill every field; pretend it worked so they don't adapt.
            if (!string.IsNullOrWhiteSpace(request.Website))
                return Results.Ok(new RegistrationResultDto(0, RegistrationStatus.Confirmed, slug));

            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest(new { message = "Name and email are required." });
            var email = request.Email.Trim().ToLowerInvariant();
            if (!IsValidEmail(email))
                return Results.BadRequest(new { message = "Please enter a valid email address." });
            if (request.FullName.Trim().Length > 200 || (request.Phone?.Length ?? 0) > 50 || (request.Note?.Length ?? 0) > 1000)
                return Results.BadRequest(new { message = "One of the fields is too long." });
            if (request.GuestsCount is < 1 or > 10)
                return Results.BadRequest(new { message = "Guests count must be between 1 and 10." });

            var ev = await db.Events.Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.Slug == slug && e.Status == EventStatus.Published);
            if (ev is null) return Results.NotFound();
            if (!ev.IsRegistrationOpen)
                return Results.BadRequest(new { message = "Registration for this event is closed." });

            var existing = ev.Registrations.FirstOrDefault(r => r.Email == email);
            if (existing is not null && existing.Status != RegistrationStatus.Cancelled)
                return Results.Conflict(new { message = "This email is already registered for this event." });

            var confirmedSeats = ev.Registrations
                .Where(r => r.Status == RegistrationStatus.Confirmed)
                .Sum(r => r.GuestsCount);
            var status = confirmedSeats + request.GuestsCount <= ev.Capacity
                ? RegistrationStatus.Confirmed
                : RegistrationStatus.Waitlisted;

            // Someone who cancelled can sign up again: (EventId, Email) is unique, so reuse the row.
            var registration = existing ?? new Registration { EventId = ev.Id, Email = email };
            registration.FullName = request.FullName.Trim();
            registration.Phone = request.Phone?.Trim();
            registration.GuestsCount = request.GuestsCount;
            registration.Note = request.Note?.Trim();
            registration.Status = status;
            registration.Language = request.Language is "nl" or "ar" ? request.Language : "en";
            registration.CreatedAt = DateTime.UtcNow; // back of the waitlist queue
            registration.CheckedInAt = null;
            registration.ManageToken ??= Guid.NewGuid();
            if (existing is null) db.Registrations.Add(registration);

            EmailOutbox.Enqueue(db, registration,
                status == RegistrationStatus.Confirmed ? EmailKind.Confirmed : EmailKind.Waitlisted);
            await db.SaveChangesAsync();
            emailSignal.Wake();

            return Results.Ok(new RegistrationResultDto(registration.Id, registration.Status, ev.Slug));
        }).RequireRateLimiting(RateLimiting.PublicForms);

        // Public: the page behind the "cancel your registration" link in emails
        app.MapGet("/api/registrations/manage/{token:guid}", async (Guid token, AppDbContext db) =>
        {
            var registration = await db.Registrations.Include(r => r.Event)
                .FirstOrDefaultAsync(r => r.ManageToken == token);
            return registration is null ? Results.NotFound() : Results.Ok(ToManagedDto(registration));
        });

        app.MapPost("/api/registrations/manage/{token:guid}/cancel",
            async (Guid token, AppDbContext db, EmailSignal emailSignal) =>
        {
            var registration = await db.Registrations
                .FirstOrDefaultAsync(r => r.ManageToken == token);
            if (registration is null) return Results.NotFound();
            var ev = await db.Events.Include(e => e.Registrations)
                .FirstAsync(e => e.Id == registration.EventId);

            if (registration.Status == RegistrationStatus.Cancelled)
                return Results.Ok(ToManagedDto(registration)); // already done; clicking twice is fine
            if (!CanCancel(registration))
                return Results.BadRequest(new { message = "This event has already taken place." });

            var previous = registration.Status;
            registration.Status = RegistrationStatus.Cancelled;
            var promoted = previous == RegistrationStatus.Confirmed ? PromoteWaitlisted(ev) : [];

            EmailOutbox.Enqueue(db, registration, EmailKind.Cancelled);
            foreach (var promotedReg in promoted)
                EmailOutbox.Enqueue(db, promotedReg, EmailKind.Promoted);
            await db.SaveChangesAsync();
            emailSignal.Wake();

            return Results.Ok(ToManagedDto(registration));
        }).RequireRateLimiting(RateLimiting.PublicForms);

        var admin = app.MapGroup("/api/admin/events/{eventId:int}/registrations").RequireAuthorization();

        admin.MapGet("/", async (int eventId, AppDbContext db) =>
        {
            var registrations = await db.Registrations
                .Where(r => r.EventId == eventId)
                .OrderBy(r => r.Status).ThenBy(r => r.CreatedAt)
                .ToListAsync();
            var lastEmails = (await db.OutgoingEmails
                    .Where(o => o.Registration!.EventId == eventId)
                    .ToListAsync())
                .GroupBy(o => o.RegistrationId)
                .ToDictionary(g => g.Key, g => g.MaxBy(o => o.Id)!);
            return registrations.Select(r => ToDto(r, lastEmails.GetValueOrDefault(r.Id)));
        });

        admin.MapPost("/{id:int}/status",
            async (int eventId, int id, StatusChange change, AppDbContext db, EmailSignal emailSignal) =>
        {
            var ev = await db.Events.Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.Id == eventId);
            var registration = ev?.Registrations.FirstOrDefault(r => r.Id == id);
            if (ev is null || registration is null) return Results.NotFound();

            var previous = registration.Status;
            registration.Status = change.Status;

            // Manual promotion from the waitlist → send the ticket
            if (previous != RegistrationStatus.Confirmed && change.Status == RegistrationStatus.Confirmed)
                EmailOutbox.Enqueue(db, registration,
                    previous == RegistrationStatus.Waitlisted ? EmailKind.Promoted : EmailKind.Confirmed);

            // Cancelling a confirmed registration frees seats → auto-promote waitlisted people who fit
            if (previous == RegistrationStatus.Confirmed && change.Status == RegistrationStatus.Cancelled)
                foreach (var promotedReg in PromoteWaitlisted(ev))
                    EmailOutbox.Enqueue(db, promotedReg, EmailKind.Promoted);

            await db.SaveChangesAsync();
            emailSignal.Wake();
            return Results.Ok(ToDto(registration));
        });

        // Send the ticket (or waitlist notice) again, e.g. when someone lost the email
        admin.MapPost("/{id:int}/resend-email",
            async (int eventId, int id, AppDbContext db, EmailSignal emailSignal) =>
        {
            var registration = await db.Registrations
                .FirstOrDefaultAsync(r => r.EventId == eventId && r.Id == id);
            if (registration is null) return Results.NotFound();
            if (registration.Status == RegistrationStatus.Cancelled)
                return Results.BadRequest(new { message = "Cancelled registrations have no ticket to send." });

            var kind = registration.Status == RegistrationStatus.Confirmed ? EmailKind.Confirmed : EmailKind.Waitlisted;
            var email = new OutgoingEmail { RegistrationId = registration.Id, Kind = kind };
            db.OutgoingEmails.Add(email);
            await db.SaveChangesAsync();
            emailSignal.Wake();
            return Results.Accepted(value: ToDto(registration, email));
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

    private static bool CanCancel(Registration r) =>
        r.Status != RegistrationStatus.Cancelled
        && r.Event is { } ev
        && ev.Date >= DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EventTimeZone.Amsterdam));

    private static ManagedRegistrationDto ToManagedDto(Registration r)
    {
        var ev = r.Event!;
        return new ManagedRegistrationDto(r.FullName, r.GuestsCount, r.Status, CanCancel(r),
            new ManagedEventDto(ev.Slug, ev.TitleEn, ev.TitleNl, ev.TitleAr,
                ev.Date, ev.StartTime, ev.EndTime, ev.Location));
    }

    private static bool IsValidEmail(string email) =>
        email.Length <= 254
        && MailAddress.TryCreate(email, out var address)
        && address.Address == email
        && address.Host.Contains('.')
        && !address.Host.StartsWith('.') && !address.Host.EndsWith('.');

    public record StatusChange(RegistrationStatus Status);

    private static string Csv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
}

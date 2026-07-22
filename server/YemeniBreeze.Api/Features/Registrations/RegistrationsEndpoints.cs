using System.Text;
using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Features.Registrations;

public record RegisterRequest(string FullName, string Email, string? Phone, int GuestsCount, string? Note);
public record RegistrationResultDto(int Id, RegistrationStatus Status, string EventSlug);
public record RegistrationDto(
    int Id, int EventId, string FullName, string Email, string? Phone,
    int GuestsCount, string? Note, RegistrationStatus Status, DateTime CreatedAt);

public static class RegistrationsEndpoints
{
    public static void MapRegistrationsEndpoints(this IEndpointRouteBuilder app)
    {
        // Public: register for an event (auto-waitlist when capacity is reached)
        app.MapPost("/api/events/{slug}/register", async (string slug, RegisterRequest request, AppDbContext db) =>
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
                Status = status
            };
            db.Registrations.Add(registration);
            await db.SaveChangesAsync();

            return Results.Ok(new RegistrationResultDto(registration.Id, registration.Status, ev.Slug));
        });

        var admin = app.MapGroup("/api/admin/events/{eventId:int}/registrations").RequireAuthorization();

        admin.MapGet("/", async (int eventId, AppDbContext db) =>
            await db.Registrations
                .Where(r => r.EventId == eventId)
                .OrderBy(r => r.Status).ThenBy(r => r.CreatedAt)
                .Select(r => new RegistrationDto(
                    r.Id, r.EventId, r.FullName, r.Email, r.Phone,
                    r.GuestsCount, r.Note, r.Status, r.CreatedAt))
                .ToListAsync());

        admin.MapPost("/{id:int}/status", async (int eventId, int id, StatusChange change, AppDbContext db) =>
        {
            var registration = await db.Registrations
                .FirstOrDefaultAsync(r => r.Id == id && r.EventId == eventId);
            if (registration is null) return Results.NotFound();
            registration.Status = change.Status;
            await db.SaveChangesAsync();
            return Results.Ok(new RegistrationDto(
                registration.Id, registration.EventId, registration.FullName, registration.Email,
                registration.Phone, registration.GuestsCount, registration.Note,
                registration.Status, registration.CreatedAt));
        });

        admin.MapGet("/export.csv", async (int eventId, AppDbContext db) =>
        {
            var rows = await db.Registrations
                .Where(r => r.EventId == eventId)
                .OrderBy(r => r.Status).ThenBy(r => r.CreatedAt)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("FullName,Email,Phone,Guests,Status,Note,RegisteredAt");
            foreach (var r in rows)
                sb.AppendLine(string.Join(',',
                    Csv(r.FullName), Csv(r.Email), Csv(r.Phone ?? ""), r.GuestsCount,
                    r.Status, Csv(r.Note ?? ""), r.CreatedAt.ToString("yyyy-MM-dd HH:mm")));

            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
                "text/csv", $"registrations-event-{eventId}.csv");
        });
    }

    public record StatusChange(RegistrationStatus Status);

    private static string Csv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
}

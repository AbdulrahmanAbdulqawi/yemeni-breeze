using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Features.Events;

public record EventDto(
    int Id, string Slug,
    string TitleEn, string TitleNl, string TitleAr,
    string DescriptionEn, string DescriptionNl, string DescriptionAr,
    DateOnly Date, string StartTime, string EndTime, string Location,
    int Capacity, string? ImageUrl, EventStatus Status, bool IsRegistrationOpen,
    int ConfirmedCount, int WaitlistedCount, int SpotsLeft);

public record EventInput(
    string Slug,
    string TitleEn, string TitleNl, string TitleAr,
    string DescriptionEn, string DescriptionNl, string DescriptionAr,
    DateOnly Date, string StartTime, string EndTime, string Location,
    int Capacity, string? ImageUrl, EventStatus Status, bool IsRegistrationOpen);

public static class EventsEndpoints
{
    public static EventDto ToDto(this Event e)
    {
        var confirmed = e.Registrations
            .Where(r => r.Status == RegistrationStatus.Confirmed)
            .Sum(r => r.GuestsCount);
        var waitlisted = e.Registrations
            .Where(r => r.Status == RegistrationStatus.Waitlisted)
            .Sum(r => r.GuestsCount);
        return new EventDto(
            e.Id, e.Slug,
            e.TitleEn, e.TitleNl, e.TitleAr,
            e.DescriptionEn, e.DescriptionNl, e.DescriptionAr,
            e.Date, e.StartTime, e.EndTime, e.Location,
            e.Capacity, e.ImageUrl, e.Status, e.IsRegistrationOpen,
            confirmed, waitlisted, Math.Max(0, e.Capacity - confirmed));
    }

    public static void MapEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var pub = app.MapGroup("/api/events");

        pub.MapGet("/", async (AppDbContext db) =>
            (await db.Events.Include(e => e.Registrations)
                .Where(e => e.Status != EventStatus.Draft)
                .OrderByDescending(e => e.Date)
                .ToListAsync())
            .Select(e => e.ToDto()));

        pub.MapGet("/{slug}", async (string slug, AppDbContext db) =>
            await db.Events.Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.Slug == slug && e.Status != EventStatus.Draft)
                is { } ev
                ? Results.Ok(ev.ToDto())
                : Results.NotFound());

        var admin = app.MapGroup("/api/admin/events").RequireAuthorization();

        admin.MapGet("/", async (AppDbContext db) =>
            (await db.Events.Include(e => e.Registrations)
                .OrderByDescending(e => e.Date)
                .ToListAsync())
            .Select(e => e.ToDto()));

        admin.MapPost("/", async (EventInput input, AppDbContext db) =>
        {
            if (await db.Events.AnyAsync(e => e.Slug == input.Slug))
                return Results.Conflict(new { message = "An event with this slug already exists." });

            var ev = new Event();
            Apply(ev, input);
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            return Results.Created($"/api/events/{ev.Slug}", ev.ToDto());
        });

        admin.MapPut("/{id:int}", async (int id, EventInput input, AppDbContext db) =>
        {
            var ev = await db.Events.Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (ev is null) return Results.NotFound();
            if (await db.Events.AnyAsync(e => e.Slug == input.Slug && e.Id != id))
                return Results.Conflict(new { message = "An event with this slug already exists." });

            Apply(ev, input);
            await db.SaveChangesAsync();
            return Results.Ok(ev.ToDto());
        });

        admin.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var ev = await db.Events.FindAsync(id);
            if (ev is null) return Results.NotFound();
            db.Events.Remove(ev);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static void Apply(Event ev, EventInput input)
    {
        ev.Slug = input.Slug.Trim().ToLowerInvariant();
        ev.TitleEn = input.TitleEn;
        ev.TitleNl = input.TitleNl;
        ev.TitleAr = input.TitleAr;
        ev.DescriptionEn = input.DescriptionEn;
        ev.DescriptionNl = input.DescriptionNl;
        ev.DescriptionAr = input.DescriptionAr;
        ev.Date = input.Date;
        ev.StartTime = input.StartTime;
        ev.EndTime = input.EndTime;
        ev.Location = input.Location;
        ev.Capacity = input.Capacity;
        ev.ImageUrl = input.ImageUrl;
        ev.Status = input.Status;
        ev.IsRegistrationOpen = input.IsRegistrationOpen;
    }
}

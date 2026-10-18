using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Storage;
using YemeniBreeze.Api.Features.Uploads;

namespace YemeniBreeze.Api.Features.Events;

public record EventDto(
    int Id, string Slug,
    string TitleEn, string TitleNl, string TitleAr,
    string DescriptionEn, string DescriptionNl, string DescriptionAr,
    DateOnly Date, string StartTime, string EndTime, string Location,
    int Capacity, string? ImageUrl, EventStatus Status, bool IsRegistrationOpen,
    int ConfirmedCount, int WaitlistedCount, int SpotsLeft,
    bool RequiresApproval, int PendingCount,
    string WhatToExpectEn, string WhatToExpectNl, string WhatToExpectAr,
    string? DonationUrl,
    // Private: filled in for the admin only, always null on the public site.
    string? ExactAddress);

public record EventInput(
    string Slug,
    string TitleEn, string TitleNl, string TitleAr,
    string DescriptionEn, string DescriptionNl, string DescriptionAr,
    DateOnly Date, string StartTime, string EndTime, string Location,
    int Capacity, string? ImageUrl, EventStatus Status, bool IsRegistrationOpen,
    bool RequiresApproval = false,
    string? WhatToExpectEn = null, string? WhatToExpectNl = null, string? WhatToExpectAr = null,
    string? DonationUrl = null,
    string? ExactAddress = null);

public static class EventsEndpoints
{
    /// <param name="includePrivate">Only for the admin: adds the exact address, which visitors must never see.</param>
    public static EventDto ToDto(this Event e, bool includePrivate = false)
    {
        var confirmed = e.Registrations
            .Where(r => r.Status == RegistrationStatus.Confirmed)
            .Sum(r => r.GuestsCount);
        var waitlisted = e.Registrations
            .Where(r => r.Status == RegistrationStatus.Waitlisted)
            .Sum(r => r.GuestsCount);
        var pending = e.Registrations
            .Where(r => r.Status == RegistrationStatus.Pending)
            .Sum(r => r.GuestsCount);
        return new EventDto(
            e.Id, e.Slug,
            e.TitleEn, e.TitleNl, e.TitleAr,
            e.DescriptionEn, e.DescriptionNl, e.DescriptionAr,
            e.Date, e.StartTime, e.EndTime, e.Location,
            e.Capacity, e.ImageUrl, e.Status, e.IsRegistrationOpen,
            confirmed, waitlisted, Math.Max(0, e.Capacity - confirmed),
            e.RequiresApproval, pending,
            e.WhatToExpectEn, e.WhatToExpectNl, e.WhatToExpectAr,
            e.DonationUrl,
            includePrivate ? e.ExactAddress : null);
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
            .Select(e => e.ToDto(includePrivate: true)));

        admin.MapPost("/", async (EventInput input, AppDbContext db) =>
        {
            if (await db.Events.AnyAsync(e => e.Slug == input.Slug))
                return Results.Conflict(new { message = "An event with this slug already exists." });
            if (!IsValidDonationUrl(input.DonationUrl)) return Results.BadRequest(InvalidDonationUrl);
            if (input.ExactAddress?.Trim().Length > 500) return Results.BadRequest(AddressTooLong);

            var ev = new Event();
            Apply(ev, input);
            db.Events.Add(ev);
            await db.SaveChangesAsync();

            db.MediaFolders.Add(new MediaFolder { Name = ev.TitleEn, EventId = ev.Id });
            await db.SaveChangesAsync();

            return Results.Created($"/api/events/{ev.Slug}", ev.ToDto(includePrivate: true));
        });

        admin.MapPut("/{id:int}", async (int id, EventInput input, AppDbContext db, StorageService storage) =>
        {
            var ev = await db.Events.Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (ev is null) return Results.NotFound();
            if (await db.Events.AnyAsync(e => e.Slug == input.Slug && e.Id != id))
                return Results.Conflict(new { message = "An event with this slug already exists." });
            if (!IsValidDonationUrl(input.DonationUrl)) return Results.BadRequest(InvalidDonationUrl);
            if (input.ExactAddress?.Trim().Length > 500) return Results.BadRequest(AddressTooLong);

            var oldImageUrl = ev.ImageUrl;
            Apply(ev, input);

            var folder = await db.MediaFolders.FirstOrDefaultAsync(f => f.EventId == ev.Id);
            if (folder is not null) folder.Name = ev.TitleEn;

            await db.SaveChangesAsync();

            if (oldImageUrl != ev.ImageUrl) await DeleteImageAsync(storage, oldImageUrl);

            return Results.Ok(ev.ToDto(includePrivate: true));
        });

        admin.MapDelete("/{id:int}", async (int id, AppDbContext db, StorageService storage) =>
        {
            var ev = await db.Events.FindAsync(id);
            if (ev is null) return Results.NotFound();
            db.Events.Remove(ev);
            await db.SaveChangesAsync();

            await DeleteImageAsync(storage, ev.ImageUrl);

            return Results.NoContent();
        });
    }

    /// <summary>Deletes the resized image and its derived thumbnail + original — Event has no
    /// ThumbUrl column, but ProcessAsync always generates a thumb, so it must be cleaned up too.</summary>
    private static async Task DeleteImageAsync(StorageService storage, string? imageUrl)
    {
        if (ImageService.KeyFromUrl(imageUrl) is { } key) await storage.DeleteAsync(key);
        if (ImageService.KeyFromUrl(ImageService.ThumbUrlFromUrl(imageUrl)) is { } thumbKey)
            await storage.DeleteAsync(thumbKey);
        if (ImageService.KeyFromUrl(ImageService.OriginalUrlFromUrl(imageUrl)) is { } originalKey)
            await storage.DeleteAsync(originalKey);
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
        ev.RequiresApproval = input.RequiresApproval;
        ev.WhatToExpectEn = input.WhatToExpectEn?.Trim() ?? "";
        ev.WhatToExpectNl = input.WhatToExpectNl?.Trim() ?? "";
        ev.WhatToExpectAr = input.WhatToExpectAr?.Trim() ?? "";
        ev.DonationUrl = string.IsNullOrWhiteSpace(input.DonationUrl) ? null : input.DonationUrl.Trim();
        ev.ExactAddress = string.IsNullOrWhiteSpace(input.ExactAddress) ? null : input.ExactAddress.Trim();
    }

    /// <summary>The donation link goes into emails, so only a full http(s) address is accepted.</summary>
    private static bool IsValidDonationUrl(string? url) =>
        string.IsNullOrWhiteSpace(url)
        || (url.Trim().Length <= 500
            && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));

    private static readonly object InvalidDonationUrl =
        new { message = "The donation link must be a full web address starting with https://" };

    private static readonly object AddressTooLong =
        new { message = "The exact address can be at most 500 characters." };
}

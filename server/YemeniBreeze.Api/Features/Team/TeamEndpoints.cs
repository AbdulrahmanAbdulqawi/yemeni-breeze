using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Storage;
using YemeniBreeze.Api.Features.Uploads;

namespace YemeniBreeze.Api.Features.Team;

public record TeamMemberDto(
    int Id, string Name, string RoleEn, string RoleNl, string RoleAr,
    string? PhotoUrl, string? BioEn, string? BioNl, string? BioAr,
    string? Slug, int SortOrder);

public record TeamMemberInput(
    string Name, string RoleEn, string RoleNl, string RoleAr,
    string? PhotoUrl, string? BioEn, string? BioNl, string? BioAr, int SortOrder);

public static class TeamEndpoints
{
    public static TeamMemberDto ToDto(this TeamMember m) => new(
        m.Id, m.Name, m.RoleEn, m.RoleNl, m.RoleAr,
        m.PhotoUrl, m.BioEn, m.BioNl, m.BioAr, m.Slug, m.SortOrder);

    public static void MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var pub = app.MapGroup("/api/team");

        pub.MapGet("/", async (AppDbContext db) =>
            (await db.TeamMembers.OrderBy(m => m.SortOrder).ToListAsync())
            .Select(m => m.ToDto()));

        pub.MapGet("/{slug}", async (string slug, AppDbContext db) =>
            await db.TeamMembers.FirstOrDefaultAsync(m => m.Slug == slug) is { } m
                ? Results.Ok(m.ToDto())
                : Results.NotFound());

        var admin = app.MapGroup("/api/admin/team").RequireAuthorization();

        admin.MapGet("/", async (AppDbContext db) =>
            (await db.TeamMembers.OrderBy(m => m.SortOrder).ToListAsync())
            .Select(m => m.ToDto()));

        admin.MapPost("/", async (TeamMemberInput input, AppDbContext db) =>
        {
            var member = new TeamMember();
            Apply(member, input);
            member.Slug = await GenerateSlugAsync(member, db);
            db.TeamMembers.Add(member);
            await db.SaveChangesAsync();
            return Results.Created($"/api/team/{member.Slug}", member.ToDto());
        });

        admin.MapPut("/{id:int}", async (int id, TeamMemberInput input, AppDbContext db, StorageService storage) =>
        {
            var member = await db.TeamMembers.FindAsync(id);
            if (member is null) return Results.NotFound();

            var oldPhotoUrl = member.PhotoUrl;
            Apply(member, input);
            member.Slug = await GenerateSlugAsync(member, db);
            await db.SaveChangesAsync();

            if (oldPhotoUrl != member.PhotoUrl) await DeletePhotoAsync(storage, oldPhotoUrl);

            return Results.Ok(member.ToDto());
        });

        admin.MapDelete("/{id:int}", async (int id, AppDbContext db, StorageService storage) =>
        {
            var member = await db.TeamMembers.FindAsync(id);
            if (member is null) return Results.NotFound();
            db.TeamMembers.Remove(member);
            await db.SaveChangesAsync();

            await DeletePhotoAsync(storage, member.PhotoUrl);

            return Results.NoContent();
        });
    }

    /// <summary>Deletes the resized photo and its derived original (see ImageService.OriginalUrlFromUrl).</summary>
    private static async Task DeletePhotoAsync(StorageService storage, string? photoUrl)
    {
        if (ImageService.KeyFromUrl(photoUrl) is { } key) await storage.DeleteAsync(key);
        if (ImageService.KeyFromUrl(ImageService.OriginalUrlFromUrl(photoUrl)) is { } originalKey)
            await storage.DeleteAsync(originalKey);
    }

    private static void Apply(TeamMember member, TeamMemberInput input)
    {
        member.Name = input.Name;
        member.RoleEn = input.RoleEn;
        member.RoleNl = input.RoleNl;
        member.RoleAr = input.RoleAr;
        member.PhotoUrl = input.PhotoUrl;
        member.BioEn = input.BioEn;
        member.BioNl = input.BioNl;
        member.BioAr = input.BioAr;
        member.SortOrder = input.SortOrder;
    }

    private static bool HasBio(TeamMember member) =>
        !string.IsNullOrWhiteSpace(member.BioEn) ||
        !string.IsNullOrWhiteSpace(member.BioNl) ||
        !string.IsNullOrWhiteSpace(member.BioAr);

    /// <summary>
    /// A member only gets a public bio page — and therefore a slug — once any bio
    /// text is filled in; clearing all bio fields clears the slug (and the page).
    /// </summary>
    private static async Task<string?> GenerateSlugAsync(TeamMember member, AppDbContext db)
    {
        if (!HasBio(member)) return null;
        if (member.Slug is not null) return member.Slug;

        var baseSlug = Regex.Replace(member.Name.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "member";

        var slug = baseSlug;
        var suffix = 2;
        while (await db.TeamMembers.AnyAsync(m => m.Slug == slug && m.Id != member.Id))
            slug = $"{baseSlug}-{suffix++}";

        return slug;
    }
}

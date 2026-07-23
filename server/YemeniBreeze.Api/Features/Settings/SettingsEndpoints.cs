using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Storage;
using YemeniBreeze.Api.Features.Uploads;

namespace YemeniBreeze.Api.Features.Settings;

public static class SettingsEndpoints
{
    private static readonly string[] AllowedKeys = ["heroImageUrl", "aboutImageUrl"];

    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings", async (AppDbContext db) =>
            await db.SiteSettings.ToDictionaryAsync(s => s.Key, s => s.Value));

        app.MapPut("/api/admin/settings", async (Dictionary<string, string> settings, AppDbContext db, StorageService storage) =>
        {
            var invalid = settings.Keys.Except(AllowedKeys).ToList();
            if (invalid.Count > 0)
                return Results.BadRequest(new { message = $"Unknown setting keys: {string.Join(", ", invalid)}" });

            var replaced = new List<string>();
            foreach (var (key, value) in settings)
            {
                var existing = await db.SiteSettings.FindAsync(key);
                if (existing is null)
                    db.SiteSettings.Add(new SiteSetting { Key = key, Value = value });
                else if (existing.Value != value)
                {
                    replaced.Add(existing.Value);
                    existing.Value = value;
                }
            }
            await db.SaveChangesAsync();

            foreach (var oldValue in replaced)
                if (ImageService.KeyFromUrl(oldValue) is { } oldKey)
                    await storage.DeleteAsync(oldKey);

            return Results.Ok(await db.SiteSettings.ToDictionaryAsync(s => s.Key, s => s.Value));
        }).RequireAuthorization();
    }
}

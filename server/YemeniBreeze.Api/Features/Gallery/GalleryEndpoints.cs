using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;
using YemeniBreeze.Api.Features.Storage;
using YemeniBreeze.Api.Features.Uploads;

namespace YemeniBreeze.Api.Features.Gallery;

public record GalleryItemDto(
    int Id, int? EventId, int? FolderId, string ImageUrl, string? ThumbUrl, string MediaType,
    string CaptionEn, string CaptionNl, string CaptionAr, int SortOrder);

public record GalleryItemInput(
    int? EventId, int? FolderId, string ImageUrl, string? ThumbUrl, string? MediaType,
    string CaptionEn, string CaptionNl, string CaptionAr, int SortOrder);

public record BulkIdsInput(int[] Ids);
public record BulkMoveInput(int[] Ids, int? FolderId);
public record BulkCaptionInput(int[] Ids, string? CaptionEn, string? CaptionNl, string? CaptionAr);

public static class GalleryEndpoints
{
    private static GalleryItemDto ToDto(GalleryItem g) =>
        new(g.Id, g.EventId, g.FolderId, g.ImageUrl, g.ThumbUrl, g.MediaType, g.CaptionEn, g.CaptionNl, g.CaptionAr, g.SortOrder);

    private static async Task DeleteMediaAsync(StorageService storage, GalleryItem item)
    {
        if (ImageService.KeyFromUrl(item.ImageUrl) is { } key) await storage.DeleteAsync(key);
        if (ImageService.KeyFromUrl(item.ThumbUrl) is { } thumbKey) await storage.DeleteAsync(thumbKey);
        if (ImageService.KeyFromUrl(ImageService.OriginalUrlFromUrl(item.ImageUrl)) is { } originalKey)
            await storage.DeleteAsync(originalKey);
    }

    public static void MapGalleryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gallery", async (AppDbContext db) =>
            (await db.GalleryItems.OrderBy(g => g.SortOrder).ThenByDescending(g => g.Id).ToListAsync())
            .Select(ToDto));

        var admin = app.MapGroup("/api/admin/gallery").RequireAuthorization();

        admin.MapPost("/", async (GalleryItemInput input, AppDbContext db) =>
        {
            var item = new GalleryItem
            {
                EventId = input.EventId,
                FolderId = input.FolderId,
                ImageUrl = input.ImageUrl,
                ThumbUrl = input.ThumbUrl,
                MediaType = input.MediaType ?? "image",
                CaptionEn = input.CaptionEn,
                CaptionNl = input.CaptionNl,
                CaptionAr = input.CaptionAr,
                SortOrder = input.SortOrder
            };
            db.GalleryItems.Add(item);
            await db.SaveChangesAsync();
            return Results.Created($"/api/gallery/{item.Id}", ToDto(item));
        });

        admin.MapPut("/{id:int}", async (int id, GalleryItemInput input, AppDbContext db) =>
        {
            var item = await db.GalleryItems.FindAsync(id);
            if (item is null) return Results.NotFound();
            item.EventId = input.EventId;
            item.FolderId = input.FolderId;
            item.ImageUrl = input.ImageUrl;
            item.ThumbUrl = input.ThumbUrl;
            item.MediaType = input.MediaType ?? item.MediaType;
            item.CaptionEn = input.CaptionEn;
            item.CaptionNl = input.CaptionNl;
            item.CaptionAr = input.CaptionAr;
            item.SortOrder = input.SortOrder;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(item));
        });

        admin.MapDelete("/{id:int}", async (int id, AppDbContext db, StorageService storage) =>
        {
            var item = await db.GalleryItems.FindAsync(id);
            if (item is null) return Results.NotFound();
            db.GalleryItems.Remove(item);
            await db.SaveChangesAsync();

            await DeleteMediaAsync(storage, item);

            return Results.NoContent();
        });

        admin.MapPost("/bulk-delete", async (BulkIdsInput input, AppDbContext db, StorageService storage) =>
        {
            var items = await db.GalleryItems.Where(g => input.Ids.Contains(g.Id)).ToListAsync();
            if (items.Count == 0) return Results.Ok(new { deleted = 0 });

            db.GalleryItems.RemoveRange(items);
            await db.SaveChangesAsync();

            foreach (var item in items) await DeleteMediaAsync(storage, item);

            return Results.Ok(new { deleted = items.Count });
        });

        admin.MapPost("/bulk-move", async (BulkMoveInput input, AppDbContext db) =>
        {
            if (input.FolderId is { } folderId && !await db.MediaFolders.AnyAsync(f => f.Id == folderId))
                return Results.BadRequest(new { message = "Folder not found." });

            var moved = await db.GalleryItems.Where(g => input.Ids.Contains(g.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.FolderId, input.FolderId));
            return Results.Ok(new { moved });
        });

        admin.MapPost("/bulk-update", async (BulkCaptionInput input, AppDbContext db) =>
        {
            var items = await db.GalleryItems.Where(g => input.Ids.Contains(g.Id)).ToListAsync();
            foreach (var item in items)
            {
                if (input.CaptionEn is not null) item.CaptionEn = input.CaptionEn;
                if (input.CaptionNl is not null) item.CaptionNl = input.CaptionNl;
                if (input.CaptionAr is not null) item.CaptionAr = input.CaptionAr;
            }
            await db.SaveChangesAsync();
            return Results.Ok(items.Select(ToDto));
        });
    }
}

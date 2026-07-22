using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Features.Gallery;

public record GalleryItemDto(
    int Id, int? EventId, string ImageUrl, string? ThumbUrl,
    string CaptionEn, string CaptionNl, string CaptionAr, int SortOrder);

public record GalleryItemInput(
    int? EventId, string ImageUrl, string? ThumbUrl,
    string CaptionEn, string CaptionNl, string CaptionAr, int SortOrder);

public static class GalleryEndpoints
{
    private static GalleryItemDto ToDto(GalleryItem g) =>
        new(g.Id, g.EventId, g.ImageUrl, g.ThumbUrl, g.CaptionEn, g.CaptionNl, g.CaptionAr, g.SortOrder);

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
                ImageUrl = input.ImageUrl,
                ThumbUrl = input.ThumbUrl,
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
            item.ImageUrl = input.ImageUrl;
            item.ThumbUrl = input.ThumbUrl;
            item.CaptionEn = input.CaptionEn;
            item.CaptionNl = input.CaptionNl;
            item.CaptionAr = input.CaptionAr;
            item.SortOrder = input.SortOrder;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(item));
        });

        admin.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var item = await db.GalleryItems.FindAsync(id);
            if (item is null) return Results.NotFound();
            db.GalleryItems.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

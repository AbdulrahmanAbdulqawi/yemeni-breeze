using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Features.Media;

public record MediaFolderDto(int Id, string Name, int? EventId, int SortOrder, int ItemCount);

public record MediaFolderInput(string Name, int? EventId, int SortOrder);

public static class FoldersEndpoints
{
    public static void MapFoldersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/folders", async (AppDbContext db) =>
            await db.MediaFolders
                .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
                .Select(f => new MediaFolderDto(f.Id, f.Name, f.EventId, f.SortOrder,
                    db.GalleryItems.Count(g => g.FolderId == f.Id)))
                .ToListAsync());

        var admin = app.MapGroup("/api/admin/folders").RequireAuthorization();

        admin.MapPost("/", async (MediaFolderInput input, AppDbContext db) =>
        {
            var name = input.Name.Trim();
            if (string.IsNullOrEmpty(name))
                return Results.BadRequest(new { message = "Folder name is required." });

            var folder = new MediaFolder { Name = name, EventId = input.EventId, SortOrder = input.SortOrder };
            db.MediaFolders.Add(folder);
            await db.SaveChangesAsync();
            return Results.Created($"/api/folders/{folder.Id}", ToDto(folder, 0));
        });

        admin.MapPut("/{id:int}", async (int id, MediaFolderInput input, AppDbContext db) =>
        {
            var folder = await db.MediaFolders.FindAsync(id);
            if (folder is null) return Results.NotFound();

            var name = input.Name.Trim();
            if (string.IsNullOrEmpty(name))
                return Results.BadRequest(new { message = "Folder name is required." });

            folder.Name = name;
            folder.EventId = input.EventId;
            folder.SortOrder = input.SortOrder;
            await db.SaveChangesAsync();

            var count = await db.GalleryItems.CountAsync(g => g.FolderId == folder.Id);
            return Results.Ok(ToDto(folder, count));
        });

        // Deleting a folder keeps its media — items fall back to "Unfiled".
        admin.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var folder = await db.MediaFolders.FindAsync(id);
            if (folder is null) return Results.NotFound();

            await db.GalleryItems.Where(g => g.FolderId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.FolderId, (int?)null));
            db.MediaFolders.Remove(folder);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static MediaFolderDto ToDto(MediaFolder f, int itemCount) =>
        new(f.Id, f.Name, f.EventId, f.SortOrder, itemCount);
}

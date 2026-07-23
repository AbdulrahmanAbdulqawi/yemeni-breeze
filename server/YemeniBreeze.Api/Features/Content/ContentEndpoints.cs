using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Features.Content;

public record ContentBlockDto(string Key, string ValueEn, string ValueNl, string ValueAr);
public record ContentBlockInput(string En, string Nl, string Ar);

public static class ContentEndpoints
{
    public static void MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/content", async (string? lang, AppDbContext db) =>
        {
            var blocks = await db.ContentBlocks.ToListAsync();
            return blocks.ToDictionary(b => b.Key, b => Localize(b, lang));
        });

        var admin = app.MapGroup("/api/admin/content").RequireAuthorization();

        admin.MapGet("/", async (AppDbContext db) =>
            (await db.ContentBlocks.OrderBy(b => b.Key).ToListAsync())
            .Select(b => new ContentBlockDto(b.Key, b.ValueEn, b.ValueNl, b.ValueAr)));

        admin.MapPut("/", async (Dictionary<string, ContentBlockInput> input, AppDbContext db) =>
        {
            foreach (var (key, value) in input)
            {
                var block = await db.ContentBlocks.FindAsync(key);
                if (block is null)
                {
                    block = new ContentBlock { Key = key };
                    db.ContentBlocks.Add(block);
                }
                block.ValueEn = value.En;
                block.ValueNl = value.Nl;
                block.ValueAr = value.Ar;
            }
            await db.SaveChangesAsync();

            return Results.Ok((await db.ContentBlocks.OrderBy(b => b.Key).ToListAsync())
                .Select(b => new ContentBlockDto(b.Key, b.ValueEn, b.ValueNl, b.ValueAr)));
        });
    }

    private static string Localize(ContentBlock block, string? lang) => lang switch
    {
        "nl" => string.IsNullOrEmpty(block.ValueNl) ? block.ValueEn : block.ValueNl,
        "ar" => string.IsNullOrEmpty(block.ValueAr) ? block.ValueEn : block.ValueAr,
        _ => block.ValueEn
    };
}

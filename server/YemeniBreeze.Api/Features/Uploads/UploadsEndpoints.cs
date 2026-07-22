using Amazon.S3;
using YemeniBreeze.Api.Features.Storage;

namespace YemeniBreeze.Api.Features.Uploads;

public record UploadedFile(string Url, string Kind, string ContentType);

public static class UploadsEndpoints
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private static readonly string[] VideoExtensions = [".mp4", ".webm", ".mov"];
    private static readonly string[] FileExtensions = [".pdf"];

    private const long MaxImageSize = 15 * 1024 * 1024;   // 15 MB (resized server-side)
    private const long MaxFileSize = 200 * 1024 * 1024;   // 200 MB (video / documents)
    private const int MaxBatchCount = 20;

    public static void MapUploadsEndpoints(this IEndpointRouteBuilder app)
    {
        // Single image → resized to WebP (large + thumb)
        app.MapPost("/api/admin/uploads", async (IFormFile file, ImageService images) =>
        {
            var error = ValidateImage(file);
            if (error is not null) return error;

            try
            {
                await using var stream = file.OpenReadStream();
                return Results.Ok(await images.ProcessAsync(stream));
            }
            catch (AmazonS3Exception ex)
            {
                return Results.Json(new
                {
                    message = "S3 upload failed",
                    errorCode = ex.ErrorCode,
                    statusCode = (int)ex.StatusCode,
                    detail = ex.Message
                }, statusCode: 502);
            }
        })
        .RequireAuthorization()
        .DisableAntiforgery();

        // Temporary S3 diagnostic
        app.MapGet("/api/admin/uploads/diag", (StorageService storage) => storage.DiagnoseAsync())
            .RequireAuthorization();

        // Multiple images → resized to WebP
        app.MapPost("/api/admin/uploads/batch", async (IFormFileCollection files, ImageService images) =>
        {
            if (files.Count is 0 or > MaxBatchCount)
                return Results.BadRequest(new { message = $"Provide 1 to {MaxBatchCount} images." });
            foreach (var file in files)
            {
                var error = ValidateImage(file);
                if (error is not null) return error;
            }

            var results = new List<ProcessedImage>();
            foreach (var file in files)
            {
                await using var stream = file.OpenReadStream();
                results.Add(await images.ProcessAsync(stream));
            }
            return Results.Ok(results);
        })
        .RequireAuthorization()
        .DisableAntiforgery();

        // Generic file / video → stored as-is (no processing)
        app.MapPost("/api/admin/uploads/file", async (IFormFile file, StorageService storage) =>
        {
            if (file.Length == 0 || file.Length > MaxFileSize)
                return Results.BadRequest(new { message = $"File must be between 1 byte and {MaxFileSize / 1024 / 1024} MB." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var kind = VideoExtensions.Contains(ext) ? "video"
                : ImageExtensions.Contains(ext) ? "image"
                : FileExtensions.Contains(ext) ? "file"
                : null;
            if (kind is null)
                return Results.BadRequest(new { message = "Allowed: images, mp4/webm/mov video, or pdf." });

            var key = $"{Guid.NewGuid():N}{ext}";
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? ContentTypes.FromExtension(ext)
                : file.ContentType;

            await using var stream = file.OpenReadStream();
            await storage.PutAsync(key, stream, contentType);

            return Results.Ok(new UploadedFile(ImageService.MediaUrl(key), kind, contentType));
        })
        .RequireAuthorization()
        .DisableAntiforgery();

        // Public media proxy — streams objects back from the (private) bucket / local disk
        app.MapGet("/api/media/{key}", async (string key, StorageService storage, HttpContext ctx) =>
        {
            if (!IsSafeKey(key)) return Results.BadRequest();
            var obj = await storage.GetAsync(key);
            if (obj is null) return Results.NotFound();

            ctx.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Results.Stream(obj.Content, obj.ContentType, enableRangeProcessing: true);
        });
    }

    private static IResult? ValidateImage(IFormFile file)
    {
        if (file.Length == 0 || file.Length > MaxImageSize)
            return Results.BadRequest(new { message = $"Each image must be between 1 byte and {MaxImageSize / 1024 / 1024} MB." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ImageExtensions.Contains(ext))
            return Results.BadRequest(new { message = "Only jpg, png, webp and gif images are allowed." });
        return null;
    }

    // Keys are GUIDs we generate (hex + one extension); reject anything with path separators.
    private static bool IsSafeKey(string key) =>
        !string.IsNullOrWhiteSpace(key) &&
        key.Length <= 100 &&
        !key.Contains('/') && !key.Contains('\\') && !key.Contains("..");
}

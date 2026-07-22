namespace YemeniBreeze.Api.Features.Uploads;

public static class UploadsEndpoints
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxFileSize = 8 * 1024 * 1024; // 8 MB

    public static void MapUploadsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/uploads", async (IFormFile file, IWebHostEnvironment env) =>
        {
            if (file.Length == 0 || file.Length > MaxFileSize)
                return Results.BadRequest(new { message = "File must be between 1 byte and 8 MB." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return Results.BadRequest(new { message = "Only jpg, png, webp and gif images are allowed." });

            var uploadsDir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
            Directory.CreateDirectory(uploadsDir);

            var name = $"{Guid.NewGuid():N}{ext}";
            await using var stream = File.Create(Path.Combine(uploadsDir, name));
            await file.CopyToAsync(stream);

            return Results.Ok(new { url = $"/uploads/{name}" });
        })
        .RequireAuthorization()
        .DisableAntiforgery();
    }
}

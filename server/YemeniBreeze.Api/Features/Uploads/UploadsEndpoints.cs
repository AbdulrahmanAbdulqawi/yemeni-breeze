namespace YemeniBreeze.Api.Features.Uploads;

public static class UploadsEndpoints
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxFileSize = 15 * 1024 * 1024; // 15 MB (phone photos, resized server-side)
    private const int MaxBatchCount = 20;

    public static void MapUploadsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/uploads", async (IFormFile file, ImageService images) =>
        {
            var error = Validate(file);
            if (error is not null) return error;

            await using var stream = file.OpenReadStream();
            var result = await images.ProcessAsync(stream);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .DisableAntiforgery();

        app.MapPost("/api/admin/uploads/batch", async (IFormFileCollection files, ImageService images) =>
        {
            if (files.Count is 0 or > MaxBatchCount)
                return Results.BadRequest(new { message = $"Provide 1 to {MaxBatchCount} images." });
            foreach (var file in files)
            {
                var error = Validate(file);
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
    }

    private static IResult? Validate(IFormFile file)
    {
        if (file.Length == 0 || file.Length > MaxFileSize)
            return Results.BadRequest(new { message = $"Each file must be between 1 byte and {MaxFileSize / 1024 / 1024} MB." });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return Results.BadRequest(new { message = "Only jpg, png, webp and gif images are allowed." });
        return null;
    }
}

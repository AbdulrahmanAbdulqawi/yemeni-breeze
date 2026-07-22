using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace YemeniBreeze.Api.Features.Uploads;

public record ProcessedImage(string Url, string ThumbUrl);

public class ImageService(IWebHostEnvironment env)
{
    private const int LargeMaxWidth = 1600;
    private const int ThumbMaxWidth = 480;

    public async Task<ProcessedImage> ProcessAsync(Stream input, CancellationToken ct = default)
    {
        var uploadsDir = Path.Combine(
            env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
        Directory.CreateDirectory(uploadsDir);

        using var image = await Image.LoadAsync(input, ct);
        image.Metadata.ExifProfile = null; // strip GPS/EXIF from phone photos

        var name = Guid.NewGuid().ToString("N");
        var largeName = $"{name}.webp";
        var thumbName = $"{name}-thumb.webp";
        var encoder = new WebpEncoder { Quality = 82 };

        if (image.Width > LargeMaxWidth)
            image.Mutate(x => x.Resize(LargeMaxWidth, 0));
        await image.SaveAsync(Path.Combine(uploadsDir, largeName), encoder, ct);

        if (image.Width > ThumbMaxWidth)
            image.Mutate(x => x.Resize(ThumbMaxWidth, 0));
        await image.SaveAsync(Path.Combine(uploadsDir, thumbName), new WebpEncoder { Quality = 78 }, ct);

        return new ProcessedImage($"/uploads/{largeName}", $"/uploads/{thumbName}");
    }
}

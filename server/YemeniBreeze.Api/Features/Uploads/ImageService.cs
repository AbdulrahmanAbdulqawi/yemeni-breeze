using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using YemeniBreeze.Api.Features.Storage;

namespace YemeniBreeze.Api.Features.Uploads;

public record ProcessedImage(string Url, string ThumbUrl);

public class ImageService(StorageService storage)
{
    private const int LargeMaxWidth = 1600;
    private const int ThumbMaxWidth = 480;

    public async Task<ProcessedImage> ProcessAsync(Stream input, CancellationToken ct = default)
    {
        using var image = await Image.LoadAsync(input, ct);
        image.Metadata.ExifProfile = null; // strip GPS/EXIF from phone photos

        var name = Guid.NewGuid().ToString("N");
        var largeKey = $"{name}.webp";
        var thumbKey = $"{name}-thumb.webp";

        if (image.Width > LargeMaxWidth)
            image.Mutate(x => x.Resize(LargeMaxWidth, 0));
        await StoreWebpAsync(image, largeKey, 82, ct);

        if (image.Width > ThumbMaxWidth)
            image.Mutate(x => x.Resize(ThumbMaxWidth, 0));
        await StoreWebpAsync(image, thumbKey, 78, ct);

        return new ProcessedImage(MediaUrl(largeKey), MediaUrl(thumbKey));
    }

    private async Task StoreWebpAsync(Image image, string key, int quality, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await image.SaveAsync(buffer, new WebpEncoder { Quality = quality }, ct);
        buffer.Position = 0;
        await storage.PutAsync(key, buffer, "image/webp", ct);
    }

    public static string MediaUrl(string key) => $"/api/media/{key}";
}

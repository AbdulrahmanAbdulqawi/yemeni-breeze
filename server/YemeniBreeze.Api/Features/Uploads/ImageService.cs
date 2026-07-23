using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using YemeniBreeze.Api.Features.Storage;

namespace YemeniBreeze.Api.Features.Uploads;

public record ProcessedImage(string Url, string ThumbUrl, string OriginalUrl);

public class ImageService(StorageService storage)
{
    private const int LargeMaxWidth = 1600;
    private const int ThumbMaxWidth = 480;
    private const string OriginalSuffix = "-original";

    public async Task<ProcessedImage> ProcessAsync(Stream input, string originalContentType, CancellationToken ct = default)
    {
        // Buffer the untouched bytes before ImageSharp loads/mutates anything, so the
        // original can be stored as-is for full-quality downloads (see OriginalUrlFromUrl).
        using var originalBuffer = new MemoryStream();
        await input.CopyToAsync(originalBuffer, ct);

        var name = Guid.NewGuid().ToString("N");
        var largeKey = $"{name}.webp";
        var thumbKey = $"{name}-thumb.webp";
        var originalKey = $"{name}{OriginalSuffix}";

        originalBuffer.Position = 0;
        await storage.PutAsync(originalKey, originalBuffer, originalContentType, ct);

        originalBuffer.Position = 0;
        using var image = await Image.LoadAsync(originalBuffer, ct);
        image.Metadata.ExifProfile = null; // strip GPS/EXIF from phone photos

        if (image.Width > LargeMaxWidth)
            image.Mutate(x => x.Resize(LargeMaxWidth, 0));
        await StoreWebpAsync(image, largeKey, 82, ct);

        if (image.Width > ThumbMaxWidth)
            image.Mutate(x => x.Resize(ThumbMaxWidth, 0));
        await StoreWebpAsync(image, thumbKey, 78, ct);

        return new ProcessedImage(MediaUrl(largeKey), MediaUrl(thumbKey), MediaUrl(originalKey));
    }

    private async Task StoreWebpAsync(Image image, string key, int quality, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await image.SaveAsync(buffer, new WebpEncoder { Quality = quality }, ct);
        buffer.Position = 0;
        await storage.PutAsync(key, buffer, "image/webp", ct);
    }

    public static string MediaUrl(string key) => $"/api/media/{key}";

    private const string MediaUrlPrefix = "/api/media/";

    public static string? KeyFromUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith(MediaUrlPrefix)
            ? url[MediaUrlPrefix.Length..]
            : null;

    /// <summary>
    /// Derives the untouched-original's media URL from a processed large-image URL
    /// (e.g. "/api/media/{name}.webp" → "/api/media/{name}-original"), by convention
    /// rather than a stored column — see ProcessAsync, which uploads it under that key.
    /// Only valid for the *large* image URL (not the "-thumb" variant).
    /// </summary>
    public static string? OriginalUrlFromUrl(string? url)
    {
        if (KeyFromUrl(url) is not { } key) return null;
        var stem = Path.GetFileNameWithoutExtension(key);
        return MediaUrl($"{stem}{OriginalSuffix}");
    }
}

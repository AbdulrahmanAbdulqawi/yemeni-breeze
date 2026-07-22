using Amazon.S3;
using Amazon.S3.Model;

namespace YemeniBreeze.Api.Features.Storage;

public class StorageOptions
{
    public string? Endpoint { get; set; }   // e.g. https://fsn1.your-objectstorage.com
    public string? Region { get; set; }     // e.g. fsn1
    public string? Bucket { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
}

public record StoredObject(Stream Content, string ContentType, long? Length);

/// <summary>
/// Stores uploaded media in an S3-compatible bucket (Hetzner Object Storage) when configured,
/// otherwise falls back to local disk under wwwroot/uploads (dev / unconfigured).
/// The bucket is private; files are read back through the API media-proxy endpoint.
/// </summary>
public class StorageService
{
    private readonly StorageOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<StorageService> _logger;
    private readonly IAmazonS3? _s3;

    public StorageService(IConfiguration config, IWebHostEnvironment env, ILogger<StorageService> logger)
    {
        _env = env;
        _logger = logger;
        _options = config.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();

        if (UseS3)
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = _options.Endpoint,
                ForcePathStyle = true, // path-style avoids per-bucket TLS/DNS issues on Hetzner
                AuthenticationRegion = _options.Region
            };
            _s3 = new AmazonS3Client(_options.AccessKey, _options.SecretKey, s3Config);
            _logger.LogInformation("StorageService: using S3 bucket '{Bucket}' at {Endpoint}",
                _options.Bucket, _options.Endpoint);
        }
        else
        {
            _logger.LogInformation("StorageService: S3 not configured, using local disk (wwwroot/uploads).");
        }
    }

    public bool UseS3 =>
        !string.IsNullOrWhiteSpace(_options.Endpoint) &&
        !string.IsNullOrWhiteSpace(_options.Bucket) &&
        !string.IsNullOrWhiteSpace(_options.AccessKey) &&
        !string.IsNullOrWhiteSpace(_options.SecretKey);

    public async Task PutAsync(string key, Stream data, string contentType, CancellationToken ct = default)
    {
        if (_s3 is not null)
        {
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = data,
                ContentType = contentType,
                DisablePayloadSigning = true // Hetzner: avoid streaming-chunked signature
            }, ct);
        }
        else
        {
            var path = LocalPath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var file = File.Create(path);
            await data.CopyToAsync(file, ct);
        }
    }

    public async Task<StoredObject?> GetAsync(string key, CancellationToken ct = default)
    {
        if (_s3 is not null)
        {
            try
            {
                var response = await _s3.GetObjectAsync(_options.Bucket, key, ct);
                return new StoredObject(response.ResponseStream, response.Headers.ContentType, response.ContentLength);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        var path = LocalPath(key);
        if (!File.Exists(path)) return null;
        return new StoredObject(File.OpenRead(path), ContentTypes.FromExtension(Path.GetExtension(path)), null);
    }

    private string LocalPath(string key)
    {
        var root = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        // keys are opaque names we generate (no slashes / traversal), safe to join
        return Path.Combine(root, "uploads", key);
    }
}

public static class ContentTypes
{
    public static string FromExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".webp" => "image/webp",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".mov" => "video/quicktime",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
}

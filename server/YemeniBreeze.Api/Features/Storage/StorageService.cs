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
                AuthenticationRegion = _options.Region,
                // Hetzner rejects the SDK's default CRC32 checksum header with InvalidArgument
                RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
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

    /// <summary>Temporary diagnostic: tries a bucket LIST under path-style and virtual-hosted addressing.</summary>
    public async Task<object> DiagnoseAsync()
    {
        if (!UseS3) return new { s3 = false };

        var regionCandidates = new[] { _options.Region ?? "hel1", "us-east-1", "eu-central-1", "eu-central", "default" };
        var results = new List<object>();
        foreach (var region in regionCandidates.Distinct())
        {
            var cfg = new AmazonS3Config
            {
                ServiceURL = _options.Endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = region,
                RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
            };
            using var client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, cfg);
            try
            {
                var r = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _options.Bucket, MaxKeys = 1 });
                results.Add(new { region, ok = true, keyCount = r.KeyCount });
            }
            catch (AmazonS3Exception ex)
            {
                results.Add(new { region, ok = false, ex.ErrorCode, status = (int)ex.StatusCode });
            }
            catch (Exception ex)
            {
                results.Add(new { region, ok = false, type = ex.GetType().Name });
            }
        }
        return new { endpoint = _options.Endpoint, bucket = _options.Bucket, results };
    }

    public async Task PutAsync(string key, Stream data, string contentType, CancellationToken ct = default)
    {
        if (_s3 is not null)
        {
            // Buffer to a seekable MemoryStream so the SDK can compute a normal SigV4
            // signed-payload hash — Hetzner rejects UNSIGNED-PAYLOAD with InvalidArgument.
            await using var buffer = new MemoryStream();
            await data.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = buffer,
                ContentType = contentType,
                AutoCloseStream = false
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

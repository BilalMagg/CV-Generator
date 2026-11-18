using Minio;
using Minio.DataModel.Args;

namespace CV_Generator.Services.Storage;

public class MinioObjectStorage : IObjectStorage, IMinioStorageService
{
    private readonly IMinioClient _client;
    private readonly string _publicBaseUrl;
    private readonly ILogger<MinioObjectStorage> _logger;
    private readonly string _endpoint;
    private readonly bool _secure;

    public MinioObjectStorage(IConfiguration config, ILogger<MinioObjectStorage> logger)
    {
        _logger = logger;

        _endpoint = config["Storage:MinIO:Endpoint"]
            ?? Environment.GetEnvironmentVariable("MINIO_ENDPOINT")
            ?? config["Minio:Endpoint"]
            ?? "localhost:9000";
        var accessKey = Environment.GetEnvironmentVariable("MINIO_ROOT_USER")
            ?? config["Storage:MinIO:AccessKey"]
            ?? config["Minio:AccessKey"]
            ?? "minioadmin";
        var secretKey = Environment.GetEnvironmentVariable("MINIO_ROOT_PASSWORD")
            ?? config["Storage:MinIO:SecretKey"]
            ?? config["Minio:SecretKey"]
            ?? "minioadmin";
        _secure = bool.TryParse(
            Environment.GetEnvironmentVariable("MINIO_SECURE")
            ?? config["Storage:MinIO:Secure"]
            ?? config["Minio:Secure"], out var s) && s;

        var host = _endpoint;
        var port = 9000;
        if (_endpoint.Contains(':'))
        {
            var parts = _endpoint.Split(':');
            host = parts[0];
            if (int.TryParse(parts[^1], out var p)) port = p;
        }

        _client = new MinioClient()
            .WithEndpoint(host, port)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(_secure)
            .Build();

        var customPublicUrl = Environment.GetEnvironmentVariable("MINIO_PUBLIC_BASE_URL")
            ?? config["Storage:MinIO:PublicBaseUrl"];

        _publicBaseUrl = !string.IsNullOrWhiteSpace(customPublicUrl)
            ? customPublicUrl.TrimEnd('/')
            : $"{(_secure ? "https" : "http")}://{_endpoint}";
    }

    public async Task EnsureBucketAsync(string bucket, CancellationToken ct = default)
    {
        try
        {
            var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
            if (!exists)
            {
                await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
                _logger.LogInformation("Created MinIO bucket '{Bucket}'", bucket);
            }

            // Only set public policy for profile images or public assets; maintain compatibility with existing setup
            await _client.SetPolicyAsync(new SetPolicyArgs().WithBucket(bucket).WithPolicy(PublicReadPolicy(bucket)), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set policy or verify bucket '{Bucket}' in MinIO", bucket);
        }
    }

    private static string PublicReadPolicy(string bucket) =>
        $$"""{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{bucket}}/*"]}]}""";

    public async Task<string> UploadAsync(string bucket, string objectKey, Stream stream, string contentType, long size, CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucket, ct);
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(size)
            .WithContentType(contentType), ct);

        return $"{_publicBaseUrl}/{bucket}/{objectKey}";
    }

    public async Task<byte[]> GetObjectAsync(string bucket, string objectKey, CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucket, ct);
        var result = Array.Empty<byte>();
        var getArgs = new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithCallbackStream(stream =>
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                result = ms.ToArray();
            });

        await _client.GetObjectAsync(getArgs, ct);
        return result;
    }

    public async Task<Stream> OpenReadStreamAsync(string bucket, string objectKey, CancellationToken ct = default)
    {
        var bytes = await GetObjectAsync(bucket, objectKey, ct);
        return new MemoryStream(bytes);
    }

    public async Task DeleteAsync(string bucket, string objectKey, CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucket, ct);
        await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(objectKey), ct);
    }

    public async Task<bool> ExistsAsync(string bucket, string objectKey, CancellationToken ct = default)
    {
        try
        {
            await _client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(objectKey), ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetPresignedUrlAsync(string bucket, string objectKey, TimeSpan expiry, bool isUpload = false, CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucket, ct);
        var seconds = Math.Max(60, (int)expiry.TotalSeconds);

        if (isUpload)
        {
            return await _client.PresignedPutObjectAsync(new PresignedPutObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey)
                .WithExpiry(seconds));
        }

        return await _client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithExpiry(seconds));
    }

    public async Task<IReadOnlyList<string>> ListObjectsAsync(string bucket, string? prefix = null, CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucket, ct);
        var keys = new List<string>();

        var args = new ListObjectsArgs()
            .WithBucket(bucket)
            .WithPrefix(prefix ?? string.Empty)
            .WithRecursive(true);

        await foreach (var item in _client.ListObjectsEnumAsync(args, ct))
        {
            if (!string.IsNullOrEmpty(item.Key))
            {
                keys.Add(item.Key);
            }
        }

        return keys;
    }

    public string NormalizeUrl(string? storedUrl)
    {
        if (string.IsNullOrWhiteSpace(storedUrl)) return string.Empty;
        if (TryResolveObject(storedUrl, out var bucket, out var key))
        {
            var scheme = _secure ? "https" : "http";
            return $"{scheme}://{_endpoint}/{bucket}/{key.TrimStart('/')}";
        }
        return storedUrl;
    }

    private static bool TryResolveObject(string url, out string bucket, out string key)
    {
        bucket = string.Empty;
        key = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var path = uri.AbsolutePath.TrimStart('/');
        var sep = path.IndexOf('/');
        if (sep <= 0 || sep == path.Length - 1) return false;
        bucket = path[..sep];
        key = path[(sep + 1)..];
        return true;
    }
}

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

namespace CV_Generator.Services.Storage;

public class CloudflareR2ObjectStorage : IObjectStorage, IMinioStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _serviceUrl;
    private readonly string? _configuredBucket;
    private readonly string? _publicBaseUrl;
    private readonly ILogger<CloudflareR2ObjectStorage> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _verifiedBuckets = new(StringComparer.OrdinalIgnoreCase);

    public CloudflareR2ObjectStorage(IConfiguration config, ILogger<CloudflareR2ObjectStorage> logger)
    {
        _logger = logger;

        var accountId = Environment.GetEnvironmentVariable("R2_ACCOUNT_ID")
            ?? config["Storage:CloudflareR2:AccountId"]
            ?? string.Empty;
        var accessKey = Environment.GetEnvironmentVariable("R2_ACCESS_KEY_ID")
            ?? config["Storage:CloudflareR2:AccessKey"]
            ?? string.Empty;
        var secretKey = Environment.GetEnvironmentVariable("R2_SECRET_ACCESS_KEY")
            ?? config["Storage:CloudflareR2:SecretKey"]
            ?? string.Empty;

        _configuredBucket = Environment.GetEnvironmentVariable("R2_BUCKET_NAME")
            ?? config["Storage:CloudflareR2:BucketName"];

        _publicBaseUrl = Environment.GetEnvironmentVariable("R2_PUBLIC_BASE_URL")
            ?? config["Storage:CloudflareR2:PublicBaseUrl"];

        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new InvalidOperationException("R2_ACCOUNT_ID or Storage:CloudflareR2:AccountId is required for Cloudflare R2 storage");
        }
        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("R2_ACCESS_KEY_ID and R2_SECRET_ACCESS_KEY are required for Cloudflare R2 storage");
        }

        _serviceUrl = $"https://{accountId}.r2.cloudflarestorage.com";

        var s3Config = new AmazonS3Config
        {
            ServiceURL = _serviceUrl,
            ForcePathStyle = true,
            SignatureVersion = "4"
        };

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        _s3Client = new AmazonS3Client(credentials, s3Config);
    }

    private (string TargetBucket, string TargetKey) ResolveLocation(string bucket, string objectKey)
    {
        var cleanKey = objectKey.TrimStart('/');
        if (!string.IsNullOrWhiteSpace(_configuredBucket))
        {
            if (string.Equals(bucket, _configuredBucket, StringComparison.OrdinalIgnoreCase))
            {
                return (_configuredBucket, cleanKey);
            }

            var prefix = bucket.Trim('/') + "/";
            if (cleanKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return (_configuredBucket, cleanKey);
            }

            return (_configuredBucket, $"{prefix}{cleanKey}");
        }

        return (bucket, cleanKey);
    }

    public async Task EnsureBucketAsync(string bucket, CancellationToken ct = default)
    {
        var targetBucket = !string.IsNullOrWhiteSpace(_configuredBucket) ? _configuredBucket : bucket;
        if (_verifiedBuckets.ContainsKey(targetBucket))
        {
            return;
        }

        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, targetBucket);
            if (!exists)
            {
                var putRequest = new PutBucketRequest { BucketName = targetBucket };
                await _s3Client.PutBucketAsync(putRequest, ct);
                _logger.LogInformation("Created Cloudflare R2 bucket '{Bucket}'", targetBucket);
            }
            _verifiedBuckets.TryAdd(targetBucket, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not verify/create bucket '{Bucket}' in Cloudflare R2 (it may already exist or require dashboard provisioning)", targetBucket);
            // Still mark verified so we don't spam failed verification calls repeatedly
            _verifiedBuckets.TryAdd(targetBucket, true);
        }
    }

    public async Task<string> UploadAsync(string bucket, string objectKey, Stream stream, string contentType, long size, CancellationToken ct = default)
    {
        var (targetBucket, targetKey) = ResolveLocation(bucket, objectKey);
        await EnsureBucketAsync(targetBucket, ct);

        var putRequest = new PutObjectRequest
        {
            BucketName = targetBucket,
            Key = targetKey,
            InputStream = stream,
            ContentType = contentType,
            DisablePayloadSigning = true
        };

        await _s3Client.PutObjectAsync(putRequest, ct);

        if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
        {
            return $"{_publicBaseUrl.TrimEnd('/')}/{targetKey}";
        }

        return $"{_serviceUrl}/{targetBucket}/{targetKey}";
    }

    public async Task<byte[]> GetObjectAsync(string bucket, string objectKey, CancellationToken ct = default)
    {
        var (targetBucket, targetKey) = ResolveLocation(bucket, objectKey);
        await EnsureBucketAsync(targetBucket, ct);

        var getRequest = new GetObjectRequest
        {
            BucketName = targetBucket,
            Key = targetKey
        };

        using var response = await _s3Client.GetObjectAsync(getRequest, ct);
        using var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    public async Task<Stream> OpenReadStreamAsync(string bucket, string objectKey, CancellationToken ct = default)
    {
        var bytes = await GetObjectAsync(bucket, objectKey, ct);
        return new MemoryStream(bytes);
    }

    public async Task DeleteAsync(string bucket, string objectKey, CancellationToken ct = default)
    {
        var (targetBucket, targetKey) = ResolveLocation(bucket, objectKey);
        await EnsureBucketAsync(targetBucket, ct);

        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = targetBucket,
            Key = targetKey
        };

        await _s3Client.DeleteObjectAsync(deleteRequest, ct);
    }

    public async Task<bool> ExistsAsync(string bucket, string objectKey, CancellationToken ct = default)
    {
        var (targetBucket, targetKey) = ResolveLocation(bucket, objectKey);
        try
        {
            var metadataRequest = new GetObjectMetadataRequest
            {
                BucketName = targetBucket,
                Key = targetKey
            };
            await _s3Client.GetObjectMetadataAsync(metadataRequest, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetPresignedUrlAsync(string bucket, string objectKey, TimeSpan expiry, bool isUpload = false, CancellationToken ct = default)
    {
        var (targetBucket, targetKey) = ResolveLocation(bucket, objectKey);
        await EnsureBucketAsync(targetBucket, ct);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = targetBucket,
            Key = targetKey,
            Verb = isUpload ? HttpVerb.PUT : HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry)
        };

        return await _s3Client.GetPreSignedURLAsync(request);
    }

    public async Task<IReadOnlyList<string>> ListObjectsAsync(string bucket, string? prefix = null, CancellationToken ct = default)
    {
        string targetBucket;
        string effectivePrefix;
        bool isSubBucketMapping = false;

        if (!string.IsNullOrWhiteSpace(_configuredBucket) &&
            !string.Equals(bucket, _configuredBucket, StringComparison.OrdinalIgnoreCase))
        {
            targetBucket = _configuredBucket;
            isSubBucketMapping = true;
            var bucketPrefix = $"{bucket.Trim('/')}/";
            effectivePrefix = string.IsNullOrEmpty(prefix) ? bucketPrefix : $"{bucketPrefix}{prefix.TrimStart('/')}";
        }
        else
        {
            targetBucket = bucket;
            effectivePrefix = prefix ?? string.Empty;
        }

        await EnsureBucketAsync(targetBucket, ct);

        var request = new ListObjectsV2Request
        {
            BucketName = targetBucket,
            Prefix = effectivePrefix
        };

        var response = await _s3Client.ListObjectsV2Async(request, ct);
        if (isSubBucketMapping)
        {
            var stripLen = $"{bucket.Trim('/')}/".Length;
            return response.S3Objects
                .Select(o => o.Key.Length > stripLen ? o.Key.Substring(stripLen) : o.Key)
                .ToList();
        }

        return response.S3Objects.Select(o => o.Key).ToList();
    }

    public string NormalizeUrl(string? storedUrl)
    {
        if (string.IsNullOrWhiteSpace(storedUrl)) return string.Empty;

        if (!string.IsNullOrWhiteSpace(_publicBaseUrl) && storedUrl.StartsWith(_publicBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return storedUrl;
        }

        if (TryResolveObject(storedUrl, out var bucket, out var key))
        {
            var cleanKey = key.TrimStart('/');
            var prefix = bucket.Trim('/') + "/";
            var targetKey = cleanKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? cleanKey
                : $"{prefix}{cleanKey}";

            if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
            {
                return $"{_publicBaseUrl.TrimEnd('/')}/{targetKey}";
            }

            return $"{_serviceUrl}/{_configuredBucket ?? bucket}/{targetKey}";
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

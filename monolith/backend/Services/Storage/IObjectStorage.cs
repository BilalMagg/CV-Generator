namespace CV_Generator.Services.Storage;

public interface IObjectStorage
{
    Task EnsureBucketAsync(string bucket, CancellationToken ct = default);
    Task<string> UploadAsync(string bucket, string objectKey, Stream stream, string contentType, long size, CancellationToken ct = default);
    Task<byte[]> GetObjectAsync(string bucket, string objectKey, CancellationToken ct = default);
    Task<Stream> OpenReadStreamAsync(string bucket, string objectKey, CancellationToken ct = default);
    Task DeleteAsync(string bucket, string objectKey, CancellationToken ct = default);
    Task<bool> ExistsAsync(string bucket, string objectKey, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string bucket, string objectKey, TimeSpan expiry, bool isUpload = false, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListObjectsAsync(string bucket, string? prefix = null, CancellationToken ct = default);
    string NormalizeUrl(string? storedUrl);
}

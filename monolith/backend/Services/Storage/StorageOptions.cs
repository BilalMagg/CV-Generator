namespace CV_Generator.Services.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "MinIO";
    public MinioStorageOptions MinIO { get; set; } = new();
    public CloudflareR2StorageOptions CloudflareR2 { get; set; } = new();

    public void Validate()
    {
        if (string.Equals(Provider, "MinIO", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(MinIO.Endpoint))
                throw new InvalidOperationException("MinIO Endpoint is required.");
        }
        else if (string.Equals(Provider, "CloudflareR2", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Provider, "R2", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(CloudflareR2.AccountId))
                throw new InvalidOperationException("Cloudflare R2 AccountId is required.");
            if (string.IsNullOrWhiteSpace(CloudflareR2.AccessKey))
                throw new InvalidOperationException("Cloudflare R2 AccessKey is required.");
            if (string.IsNullOrWhiteSpace(CloudflareR2.SecretKey))
                throw new InvalidOperationException("Cloudflare R2 SecretKey is required.");
        }
        else
        {
            throw new InvalidOperationException($"Unsupported storage provider: '{Provider}'");
        }
    }
}

public class MinioStorageOptions
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public bool Secure { get; set; } = false;
    public string? PublicBaseUrl { get; set; }
}

public class CloudflareR2StorageOptions
{
    public string AccountId { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string? BucketName { get; set; }
    public string? PublicBaseUrl { get; set; }
}

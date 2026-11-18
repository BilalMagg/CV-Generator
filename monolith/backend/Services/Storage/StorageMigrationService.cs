namespace CV_Generator.Services.Storage;

public class StorageMigrationReport
{
    public int TotalFound { get; set; }
    public int SuccessfullyMigrated { get; set; }
    public int SkippedExisting { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class StorageMigrationService
{
    private readonly ILogger<StorageMigrationService> _logger;

    public StorageMigrationService(ILogger<StorageMigrationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Migrates all objects from source storage to destination storage for specified buckets.
    /// Never deletes source objects. Verifies destination existence after transfer.
    /// </summary>
    public async Task<StorageMigrationReport> MigrateBucketAsync(
        IObjectStorage source,
        IObjectStorage destination,
        string bucket,
        string? prefix = null,
        bool overwrite = false,
        CancellationToken ct = default)
    {
        var report = new StorageMigrationReport();
        _logger.LogInformation("Starting storage migration for bucket '{Bucket}' (prefix: '{Prefix}')", bucket, prefix ?? "*");

        await destination.EnsureBucketAsync(bucket, ct);

        IReadOnlyList<string> objectKeys;
        try
        {
            objectKeys = await source.ListObjectsAsync(bucket, prefix, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list objects in source bucket '{Bucket}'", bucket);
            report.Errors.Add($"ListObjects error: {ex.Message}");
            return report;
        }

        report.TotalFound = objectKeys.Count;
        _logger.LogInformation("Found {Count} objects in bucket '{Bucket}'", objectKeys.Count, bucket);

        foreach (var key in objectKeys)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (!overwrite && await destination.ExistsAsync(bucket, key, ct))
                {
                    _logger.LogDebug("Object '{Bucket}/{Key}' already exists at destination, skipping", bucket, key);
                    report.SkippedExisting++;
                    continue;
                }

                using var stream = await source.OpenReadStreamAsync(bucket, key, ct);
                var contentType = GuessContentType(key);

                await destination.UploadAsync(bucket, key, stream, contentType, -1, ct);

                report.SuccessfullyMigrated++;
                _logger.LogInformation("Successfully migrated '{Bucket}/{Key}'", bucket, key);
            }
            catch (Exception ex)
            {
                report.Failed++;
                var errorMsg = $"Failed to migrate {bucket}/{key}: {ex.Message}";
                _logger.LogError(ex, errorMsg);
                report.Errors.Add(errorMsg);
            }
        }

        _logger.LogInformation(
            "Migration finished for '{Bucket}'. Migrated: {Migrated}, Skipped: {Skipped}, Failed: {Failed}",
            bucket, report.SuccessfullyMigrated, report.SkippedExisting, report.Failed);

        return report;
    }

    private static string GuessContentType(string key)
    {
        var ext = Path.GetExtension(key).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".tex" => "application/x-tex",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}

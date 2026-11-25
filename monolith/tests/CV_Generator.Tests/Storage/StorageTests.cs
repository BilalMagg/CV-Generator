using CV_Generator.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CV_Generator.Tests.Storage;

public class StorageTests
{
    [Fact]
    public void StorageOptions_Validation_RequiresValidProvider()
    {
        var options = new StorageOptions
        {
            Provider = "InvalidProvider"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Unsupported storage provider", ex.Message);
    }

    [Fact]
    public void StorageOptions_CloudflareR2_RequiresCredentials()
    {
        var options = new StorageOptions
        {
            Provider = "CloudflareR2",
            CloudflareR2 = new CloudflareR2StorageOptions
            {
                AccountId = "",
                AccessKey = "test",
                SecretKey = "test"
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("AccountId", ex.Message);
    }

    [Fact]
    public void StorageOptions_MinIO_RequiresEndpointAndCredentials()
    {
        var options = new StorageOptions
        {
            Provider = "MinIO",
            MinIO = new MinioStorageOptions
            {
                Endpoint = "",
                AccessKey = "",
                SecretKey = ""
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Endpoint", ex.Message);
    }

    [Fact]
    public async Task StorageMigrationService_MigratesObjectsBetweenProviders_WithoutDeletingSource()
    {
        // Arrange
        var source = Substitute.For<IObjectStorage>();
        var destination = Substitute.For<IObjectStorage>();
        var logger = Substitute.For<ILogger<StorageMigrationService>>();

        var objectKeys = new List<string> { "file1.pdf", "file2.png" };

        source.ListObjectsAsync("propel", "user-123/", Arg.Any<CancellationToken>())
            .Returns(objectKeys);

        destination.ExistsAsync("propel", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        source.OpenReadStreamAsync("propel", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new MemoryStream(new byte[] { 1, 2, 3 }));

        var migrationService = new StorageMigrationService(logger);

        // Act
        var result = await migrationService.MigrateBucketAsync(source, destination, "propel", "user-123/");

        // Assert
        Assert.Equal(2, result.TotalFound);
        Assert.Equal(2, result.SuccessfullyMigrated);
        Assert.Equal(0, result.Failed);
        Assert.Equal(0, result.SkippedExisting);

        // Ensure destination received uploads
        await destination.Received(2).UploadAsync(
            "propel",
            Arg.Any<string>(),
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());

        // Source MUST NEVER be deleted
        await source.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StorageMigrationService_SkipsExistingFiles_WhenOverwriteIsFalse()
    {
        var source = Substitute.For<IObjectStorage>();
        var destination = Substitute.For<IObjectStorage>();
        var logger = Substitute.For<ILogger<StorageMigrationService>>();

        var objectKeys = new List<string> { "file1.pdf" };

        source.ListObjectsAsync("propel", "prefix/", Arg.Any<CancellationToken>())
            .Returns(objectKeys);

        destination.ExistsAsync("propel", "file1.pdf", Arg.Any<CancellationToken>())
            .Returns(true);

        var migrationService = new StorageMigrationService(logger);

        var result = await migrationService.MigrateBucketAsync(source, destination, "propel", "prefix/", overwrite: false);

        Assert.Equal(1, result.TotalFound);
        Assert.Equal(0, result.SuccessfullyMigrated);
        Assert.Equal(1, result.SkippedExisting);

        await destination.DidNotReceive().UploadAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CloudflareR2ObjectStorage_NormalizeUrl_TransformsMinioUrlsToR2()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:CloudflareR2:AccountId"] = "dummy-account",
                ["Storage:CloudflareR2:AccessKey"] = "dummy-key",
                ["Storage:CloudflareR2:SecretKey"] = "dummy-secret",
                ["Storage:CloudflareR2:BucketName"] = "propel",
                ["Storage:CloudflareR2:PublicBaseUrl"] = "https://pub-3c1aebafae2648bc8c6454a7d2915430.r2.dev"
            }).Build();

        var logger = Substitute.For<ILogger<CloudflareR2ObjectStorage>>();
        var storage = new CloudflareR2ObjectStorage(config, logger);

        var oldMinioUrl = "http://192.168.1.110:9000/profile-images/user1/image1.png";
        var normalized = storage.NormalizeUrl(oldMinioUrl);

        Assert.Equal("https://pub-3c1aebafae2648bc8c6454a7d2915430.r2.dev/profile-images/user1/image1.png", normalized);
    }
}

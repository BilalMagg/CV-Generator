using CV_Generator.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace StorageMigrator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("==================================================================");
        Console.WriteLine(" Propel — Storage Migration Tool (MinIO -> Cloudflare R2)");
        Console.WriteLine("==================================================================");

        var repoRoot = FindRepoRoot();
        var envPath = Path.Combine(repoRoot, "deploy", ".env");
        LoadEnvFile(envPath);

        // MinIO settings
        var minioEndpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT") ?? "localhost:9000";
        if (minioEndpoint == "minio:9000" || string.IsNullOrWhiteSpace(minioEndpoint))
        {
            minioEndpoint = "localhost:9000";
        }
        Environment.SetEnvironmentVariable("MINIO_ENDPOINT", minioEndpoint);
        var minioUser = Environment.GetEnvironmentVariable("MINIO_ROOT_USER") ?? "minioadmin";
        var minioPass = Environment.GetEnvironmentVariable("MINIO_ROOT_PASSWORD") ?? "minioadmin";

        // Cloudflare R2 settings
        var r2AccountId = Environment.GetEnvironmentVariable("R2_ACCOUNT_ID") ?? "";
        var r2AccessKey = Environment.GetEnvironmentVariable("R2_ACCESS_KEY_ID") ?? "";
        var r2SecretKey = Environment.GetEnvironmentVariable("R2_SECRET_ACCESS_KEY") ?? "";
        var r2BucketName = Environment.GetEnvironmentVariable("R2_BUCKET_NAME") ?? "propel";

        if (string.IsNullOrWhiteSpace(r2AccountId) || string.IsNullOrWhiteSpace(r2AccessKey) || string.IsNullOrWhiteSpace(r2SecretKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] Missing Cloudflare R2 credentials in deploy/.env");
            Console.WriteLine("Ensure R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, and R2_SECRET_ACCESS_KEY are set.");
            Console.ResetColor();
            return 1;
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var minioLogger = loggerFactory.CreateLogger<MinioObjectStorage>();
        var r2Logger = loggerFactory.CreateLogger<CloudflareR2ObjectStorage>();
        var migratorLogger = loggerFactory.CreateLogger<StorageMigrationService>();

        var minioConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:MinIO:Endpoint"] = minioEndpoint,
            ["Storage:MinIO:AccessKey"] = minioUser,
            ["Storage:MinIO:SecretKey"] = minioPass,
            ["Storage:MinIO:Secure"] = "false"
        }).Build();

        var r2Config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:CloudflareR2:AccountId"] = r2AccountId,
            ["Storage:CloudflareR2:AccessKey"] = r2AccessKey,
            ["Storage:CloudflareR2:SecretKey"] = r2SecretKey,
            ["Storage:CloudflareR2:BucketName"] = r2BucketName
        }).Build();

        Console.WriteLine($"\n[Source]      MinIO Endpoint : http://{minioEndpoint}");
        Console.WriteLine($"[Destination] Cloudflare R2  : Account {r2AccountId}, Bucket '{r2BucketName}'");

        MinioObjectStorage minioStorage;
        CloudflareR2ObjectStorage r2Storage;

        try
        {
            minioStorage = new MinioObjectStorage(minioConfig, minioLogger);
            r2Storage = new CloudflareR2ObjectStorage(r2Config, r2Logger);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] Failed to initialize storage providers: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        // Pre-flight check on R2 write permissions
        Console.WriteLine("\n--> Performing pre-flight check on Cloudflare R2 write permissions...");
        try
        {
            using var probeStream = new MemoryStream("probe"u8.ToArray());
            await r2Storage.UploadAsync(r2BucketName, ".probe-write-check", probeStream, "text/plain", 5);
            await r2Storage.DeleteAsync(r2BucketName, ".probe-write-check");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[OK] Cloudflare R2 write permissions verified.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[PRE-FLIGHT FAILED] Cloudflare R2 rejected upload: {ex.Message}");
            Console.WriteLine("\n[DIAGNOSIS]");
            Console.WriteLine("Your Cloudflare R2 API Token does NOT have Write permissions on bucket '" + r2BucketName + "'.");
            Console.WriteLine("When you created the API token in Cloudflare Dashboard, it was likely created as 'Object Read-Only'.");
            Console.WriteLine("\n[HOW TO FIX]");
            Console.WriteLine("1. Open Cloudflare Dashboard -> R2 Object Storage -> Manage R2 API Tokens.");
            Console.WriteLine("2. Click 'Create API Token'.");
            Console.WriteLine("3. Under Permissions, select 'Object Read & Write' (or 'Admin Read & Write').");
            Console.WriteLine("4. Under Bucket, select 'Apply to specific bucket' -> '" + r2BucketName + "' (or 'All buckets').");
            Console.WriteLine("5. Copy the new Access Key ID and Secret Access Key into deploy/.env.");
            Console.WriteLine("6. Re-run this migration tool.");
            Console.ResetColor();
            return 2;
        }

        if (args.Contains("--diagnose"))
        {
            await RunDiagnosticsAsync(r2Storage);
            return 0;
        }

        var buckets = new[]
        {
            "cv-artifacts",
            "cover-letter-artifacts",
            "profile-images",
            "mail-attachments"
        };

        var migrationService = new StorageMigrationService(migratorLogger);
        var totalFoundAll = 0;
        var totalMigratedAll = 0;
        var totalSkippedAll = 0;
        var totalFailedAll = 0;

        Console.WriteLine("\n--> Beginning non-destructive migration of all buckets...\n");

        for (int i = 0; i < buckets.Length; i++)
        {
            var bucket = buckets[i];
            Console.WriteLine($"------------------------------------------------------------------");
            Console.WriteLine($" [{i + 1}/{buckets.Length}] Processing bucket '{bucket}'...");
            Console.WriteLine($"------------------------------------------------------------------");

            try
            {
                var report = await migrationService.MigrateBucketAsync(minioStorage, r2Storage, bucket, overwrite: false);

                totalFoundAll += report.TotalFound;
                totalMigratedAll += report.SuccessfullyMigrated;
                totalSkippedAll += report.SkippedExisting;
                totalFailedAll += report.Failed;

                Console.WriteLine($"   Result for '{bucket}':");
                Console.WriteLine($"   - Found       : {report.TotalFound}");
                Console.WriteLine($"   - Migrated    : {report.SuccessfullyMigrated}");
                Console.WriteLine($"   - Skipped (already in R2) : {report.SkippedExisting}");
                Console.WriteLine($"   - Failed      : {report.Failed}");

                if (report.Errors.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    foreach (var err in report.Errors.Take(5))
                    {
                        Console.WriteLine($"     * {err}");
                    }
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"   [ERROR] Failed migrating bucket '{bucket}': {ex.Message}");
                Console.ResetColor();
            }
        }

        Console.WriteLine("\n==================================================================");
        Console.WriteLine(" MIGRATION SUMMARY");
        Console.WriteLine("==================================================================");
        Console.WriteLine($" Total Objects Found     : {totalFoundAll}");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($" Successfully Migrated   : {totalMigratedAll}");
        Console.ResetColor();
        Console.WriteLine($" Already in R2 (Skipped) : {totalSkippedAll}");
        if (totalFailedAll > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($" Failed Transfers        : {totalFailedAll}");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($" Failed Transfers        : 0");
        }
        Console.WriteLine(" Source MinIO data status: 100% UNTOUCHED (safe & intact)");
        Console.WriteLine("==================================================================");

        return totalFailedAll > 0 ? 1 : 0;
    }

    private static string FindRepoRoot()
    {
        var current = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "deploy", ".env")) ||
                File.Exists(Path.Combine(current, "deploy", "docker-compose.yml")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }

    private static void LoadEnvFile(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
            var idx = trimmed.IndexOf('=');
            if (idx <= 0) continue;
            var key = trimmed.Substring(0, idx).Trim();
            var val = trimmed.Substring(idx + 1).Trim();
            if (val.StartsWith('"') && val.EndsWith('"') && val.Length >= 2)
            {
                val = val.Substring(1, val.Length - 2);
            }
            Environment.SetEnvironmentVariable(key, val);
        }
    }

    private static async Task RunDiagnosticsAsync(CloudflareR2ObjectStorage r2Storage)
    {
        Console.WriteLine("\n[DIAGNOSTICS] Testing Cloudflare R2 listing & DB URL resolution...");

        // 1. List objects in R2
        Console.WriteLine("\n--- Cloudflare R2: First 20 objects in 'propel' bucket ---");
        var objects = await r2Storage.ListObjectsAsync("propel");
        Console.WriteLine($"Total objects in R2: {objects.Count}");
        foreach (var obj in objects.Take(20))
        {
            Console.WriteLine($" - {obj}");
        }

        // 2. Connect to postgres (read-only)
        var connStr = Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=cv_monolith;Username=postgres;Password=bime";

        try
        {
            await using var conn = new Npgsql.NpgsqlConnection(connStr);
            await conn.OpenAsync();
            Console.WriteLine("\n[OK] Connected to PostgreSQL: " + conn.Database);

            // List tables in cv_monolith
            Console.WriteLine("\n--- Tables in Database ---");
            await using (var cmd = new Npgsql.NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema='public';", conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    Console.Write(reader.GetString(0) + ", ");
                }
                Console.WriteLine();
            }

            // 3. Check CvVersions
            Console.WriteLine("\n--- Database: \"CvVersions\" sample ---");
            await using (var cmd = new Npgsql.NpgsqlCommand("SELECT \"Id\", \"CvId\", \"FileUrl\", \"PdfUrl\", \"ThumbnailUrl\" FROM \"CvVersions\" LIMIT 5;", conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetGuid(0);
                    var cvId = reader.GetGuid(1);
                    var fileUrl = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var pdfUrl = reader.IsDBNull(3) ? null : reader.GetString(3);
                    var thumbUrl = reader.IsDBNull(4) ? null : reader.GetString(4);

                    Console.WriteLine($"Version {id}:");
                    Console.WriteLine($"  fileUrl  : {fileUrl}");
                    Console.WriteLine($"  pdfUrl   : {pdfUrl}");
                    Console.WriteLine($"  thumbUrl : {thumbUrl}");

                    var testUrl = pdfUrl ?? fileUrl;
                    if (!string.IsNullOrEmpty(testUrl) && TryResolveObject(testUrl, out var b, out var k))
                    {
                        Console.WriteLine($"  -> Resolved: bucket='{b}', key='{k}'");
                        var exists = await r2Storage.ExistsAsync(b, k);
                        Console.WriteLine($"  -> R2 Exists: {exists}");
                        if (exists)
                        {
                            var bytes = await r2Storage.GetObjectAsync(b, k);
                            Console.WriteLine($"  -> R2 Download OK ({bytes.Length} bytes)");
                        }
                    }
                }
            }

            // 4. Check user_images
            Console.WriteLine("\n--- Database: user_images columns & sample ---");
            await using (var cmdCols = new Npgsql.NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_name='user_images';", conn))
            await using (var rdr = await cmdCols.ExecuteReaderAsync())
            {
                Console.Write("Columns: ");
                while (await rdr.ReadAsync()) Console.Write(rdr.GetString(0) + ", ");
                Console.WriteLine();
            }

            // 5. Check companies
            Console.WriteLine("\n--- Database: companies sample ---");
            await using (var cmd = new Npgsql.NpgsqlCommand("SELECT \"Id\", \"Name\", \"LogoImageId\", \"LogoUrl\" FROM companies WHERE \"LogoUrl\" IS NOT NULL OR \"LogoImageId\" IS NOT NULL LIMIT 5;", conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetGuid(0);
                    var name = reader.GetString(1);
                    var imgId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
                    var logoUrl = reader.IsDBNull(3) ? null : reader.GetString(3);

                    Console.WriteLine($"Company {id} ({name}):");
                    Console.WriteLine($"  LogoImageId : {imgId}");
                    Console.WriteLine($"  LogoUrl     : {logoUrl}");
                    Console.WriteLine($"  Normalized  : {r2Storage.NormalizeUrl(logoUrl)}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[DB ERROR] " + ex.Message);
        }
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

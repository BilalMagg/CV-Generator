using CV_Generator.Services.Storage;

namespace CV_Generator.Services;

/// <summary>
/// Transitional alias interface for backward compatibility.
/// All new code should inject <see cref="IObjectStorage"/>.
/// </summary>
public interface IMinioStorageService : IObjectStorage
{
}

/// <summary>
/// Backward-compatible wrapper delegating to <see cref="MinioObjectStorage"/>.
/// </summary>
public class MinioStorageService : MinioObjectStorage
{
    public MinioStorageService(IConfiguration config, ILogger<MinioObjectStorage> logger)
        : base(config, logger)
    {
    }
}

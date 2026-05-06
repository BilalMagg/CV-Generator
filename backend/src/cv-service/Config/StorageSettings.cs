namespace CvService.Config;

// CV Service configuration models
// e.g., StorageSettings, MinIOConfig, CvTemplateSettings

public class StorageSettings
{
    public string Provider { get; set; } = "MinIO";
    public string Endpoint { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string BucketName { get; set; } = "cv-storage";
}

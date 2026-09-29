namespace Cinema.Foundation.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Local"; // "Local" | "S3"
    public LocalStorageOptions Local { get; set; } = new();
    public S3StorageOptions S3 { get; set; } = new();
}

public class LocalStorageOptions
{
    public string StoragePath { get; set; } = "App_Data/storage";
    public string BasePublicUrl { get; set; } = "/api/v1/media/files";
    public string SigningSecret { get; set; } = "cinema-local-storage-presign-signing-secret-default-key-32chars!";
}

public class S3StorageOptions
{
    public string? ServiceURL { get; set; }
    public string BucketName { get; set; } = "cinema-media";
    public string Region { get; set; } = "ap-southeast-1";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool ForcePathStyle { get; set; } = false;
    public string? PublicBaseUrl { get; set; }
}

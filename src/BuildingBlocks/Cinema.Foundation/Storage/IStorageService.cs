namespace Cinema.Foundation.Storage;

public interface IStorageService
{
    string ProviderName { get; }
    Task<StorageResult> UploadAsync(Stream stream, string fileName, string contentType, string category, CancellationToken ct = default);
    Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(string fileName, string contentType, string category, TimeSpan expiresIn, CancellationToken ct = default);
    Task<bool> DeleteAsync(string storageKey, CancellationToken ct = default);
    Task<StorageFileResult?> GetFileAsync(string storageKey, CancellationToken ct = default);
}

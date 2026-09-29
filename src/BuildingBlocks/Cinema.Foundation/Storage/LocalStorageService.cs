using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cinema.Foundation.Storage;

public class LocalStorageService : IStorageService
{
    private readonly StorageOptions _options;
    private readonly ILogger<LocalStorageService>? _logger;
    private readonly string _storageRoot;

    public string ProviderName => "Local";

    public LocalStorageService(IOptions<StorageOptions> options, ILogger<LocalStorageService>? logger = null)
    {
        _options = options.Value;
        _logger = logger;

        var configuredPath = string.IsNullOrWhiteSpace(_options.Local.StoragePath)
            ? "App_Data/storage"
            : _options.Local.StoragePath;

        _storageRoot = Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));

        Directory.CreateDirectory(_storageRoot);
    }

    public string StorageRoot => _storageRoot;

    public async Task<StorageResult> UploadAsync(
        Stream stream, 
        string fileName, 
        string contentType, 
        string category, 
        CancellationToken ct = default)
    {
        var safeCategory = MediaFileValidator.SanitizeFileName(category.ToLowerInvariant());
        var safeFileName = MediaFileValidator.SanitizeFileName(fileName);
        var uniqueFileName = $"{Guid.NewGuid():N}_{safeFileName}";

        var categoryDir = Path.Combine(_storageRoot, safeCategory);
        Directory.CreateDirectory(categoryDir);

        var fullPath = Path.GetFullPath(Path.Combine(categoryDir, uniqueFileName));
        EnsureWithinRoot(fullPath);

        if (stream.CanSeek && stream.Position != 0)
        {
            stream.Position = 0;
        }

        await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await stream.CopyToAsync(fileStream, ct);
        }

        var fileInfo = new FileInfo(fullPath);
        var storageKey = $"{safeCategory}/{uniqueFileName}";
        var publicUrl = BuildPublicUrl(storageKey);

        _logger?.LogInformation("Saved local media file to {Path} under key {Key}", fullPath, storageKey);

        return new StorageResult(publicUrl, storageKey, contentType, fileInfo.Length);
    }

    public Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(
        string fileName, 
        string contentType, 
        string category, 
        TimeSpan expiresIn, 
        CancellationToken ct = default)
    {
        var safeCategory = MediaFileValidator.SanitizeFileName(category.ToLowerInvariant());
        var safeFileName = MediaFileValidator.SanitizeFileName(fileName);
        var uniqueFileName = $"{Guid.NewGuid():N}_{safeFileName}";
        var storageKey = $"{safeCategory}/{uniqueFileName}";

        var expiresAt = DateTimeOffset.UtcNow.Add(expiresIn);
        var expSeconds = expiresAt.ToUnixTimeSeconds();
        var signature = GenerateSignature(storageKey, expSeconds);

        var basePublicUrl = _options.Local.BasePublicUrl.TrimEnd('/');
        var publicUrl = BuildPublicUrl(storageKey);
        var uploadUrl = $"{basePublicUrl}/{storageKey}?sig={signature}&exp={expSeconds}";

        return Task.FromResult(new PresignedUploadResult(uploadUrl, publicUrl, storageKey, expiresAt));
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var normalizedKey = NormalizeStorageKey(storageKey);
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, normalizedKey));

        if (!IsWithinRoot(fullPath) || !File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(fullPath);
            _logger?.LogInformation("Deleted local media file at {Path}", fullPath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete local media file at {Path}", fullPath);
            return Task.FromResult(false);
        }
    }

    public Task<StorageFileResult?> GetFileAsync(string storageKey, CancellationToken ct = default)
    {
        var normalizedKey = NormalizeStorageKey(storageKey);
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, normalizedKey));

        if (!IsWithinRoot(fullPath) || !File.Exists(fullPath))
        {
            return Task.FromResult<StorageFileResult?>(null);
        }

        var fileInfo = new FileInfo(fullPath);
        var ext = Path.GetExtension(fullPath);
        var contentType = MediaFileValidator.GetContentTypeForExtension(ext);
        var etag = $"\"{fileInfo.LastWriteTimeUtc.Ticks:x}-{fileInfo.Length:x}\"";

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var result = new StorageFileResult(stream, contentType, etag, fileInfo.LastWriteTimeUtc, fileInfo.Length);

        return Task.FromResult<StorageFileResult?>(result);
    }

    public bool ValidatePresignedSignature(string storageKey, long expSeconds, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (expSeconds < now) return false;

        var expectedSignature = GenerateSignature(storageKey, expSeconds);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(signature));
    }

    private string GenerateSignature(string storageKey, long expSeconds)
    {
        var secret = string.IsNullOrWhiteSpace(_options.Local.SigningSecret)
            ? "cinema-local-storage-presign-signing-secret-default-key-32chars!"
            : _options.Local.SigningSecret;

        var payload = $"{storageKey}:{expSeconds}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string BuildPublicUrl(string storageKey)
    {
        var basePublicUrl = _options.Local.BasePublicUrl.TrimEnd('/');
        return $"{basePublicUrl}/{storageKey}";
    }

    private static string NormalizeStorageKey(string storageKey)
    {
        return storageKey.Replace('\\', '/').TrimStart('/');
    }

    public bool IsWithinRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(_storageRoot, fullPath);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    public void EnsureWithinRoot(string path)
    {
        if (!IsWithinRoot(path))
        {
            throw new InvalidOperationException($"Access outside root storage directory is blocked: {path}");
        }
    }
}

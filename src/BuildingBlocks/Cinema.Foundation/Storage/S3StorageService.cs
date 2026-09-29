using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cinema.Foundation.Storage;

public class S3StorageService : IStorageService, IDisposable
{
    private readonly StorageOptions _options;
    private readonly ILogger<S3StorageService>? _logger;
    private readonly IAmazonS3 _s3Client;
    private readonly bool _ownsClient;

    public string ProviderName => "S3";

    public S3StorageService(
        IOptions<StorageOptions> options, 
        ILogger<S3StorageService>? logger = null, 
        IAmazonS3? s3Client = null)
    {
        _options = options.Value;
        _logger = logger;

        if (s3Client != null)
        {
            _s3Client = s3Client;
            _ownsClient = false;
        }
        else
        {
            var s3 = _options.S3;
            var config = new AmazonS3Config
            {
                ForcePathStyle = s3.ForcePathStyle
            };

            if (!string.IsNullOrWhiteSpace(s3.ServiceURL))
            {
                config.ServiceURL = s3.ServiceURL;
                config.ForcePathStyle = true;
            }
            else if (!string.IsNullOrWhiteSpace(s3.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(s3.Region);
            }

            AWSCredentials credentials = (!string.IsNullOrWhiteSpace(s3.AccessKey) && !string.IsNullOrWhiteSpace(s3.SecretKey))
                ? new BasicAWSCredentials(s3.AccessKey, s3.SecretKey)
                : new AnonymousAWSCredentials();

            _s3Client = new AmazonS3Client(credentials, config);
            _ownsClient = true;
        }
    }

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
        var storageKey = $"{safeCategory}/{uniqueFileName}";

        long sizeBytes = 0;
        try
        {
            if (stream.CanSeek) sizeBytes = stream.Length;
        }
        catch { }

        if (stream.CanSeek && stream.Position != 0)
        {
            stream.Position = 0;
        }

        var putRequest = new PutObjectRequest
        {
            BucketName = _options.S3.BucketName,
            Key = storageKey,
            InputStream = stream,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(putRequest, ct);

        var publicUrl = BuildPublicUrl(storageKey);
        _logger?.LogInformation("Uploaded media object to S3 bucket {Bucket} key {Key}", _options.S3.BucketName, storageKey);

        return new StorageResult(publicUrl, storageKey, contentType, sizeBytes);
    }

    public async Task<PresignedUploadResult> GeneratePresignedUploadUrlAsync(
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

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.S3.BucketName,
            Key = storageKey,
            Verb = HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            ContentType = contentType
        };

        var uploadUrl = await _s3Client.GetPreSignedURLAsync(request);
        var publicUrl = BuildPublicUrl(storageKey);

        _logger?.LogInformation("Generated presigned upload URL for S3 key {Key} expires at {ExpiresAt}", storageKey, expiresAt);

        return new PresignedUploadResult(uploadUrl, publicUrl, storageKey, expiresAt);
    }

    public async Task<bool> DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var normalizedKey = storageKey.Replace('\\', '/').TrimStart('/');

        try
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _options.S3.BucketName,
                Key = normalizedKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, ct);
            _logger?.LogInformation("Deleted S3 object {Key} from bucket {Bucket}", normalizedKey, _options.S3.BucketName);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger?.LogWarning("S3 object {Key} not found in bucket {Bucket}", normalizedKey, _options.S3.BucketName);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete S3 object {Key} from bucket {Bucket}", normalizedKey, _options.S3.BucketName);
            return false;
        }
    }

    public async Task<StorageFileResult?> GetFileAsync(string storageKey, CancellationToken ct = default)
    {
        var normalizedKey = storageKey.Replace('\\', '/').TrimStart('/');

        try
        {
            var getRequest = new GetObjectRequest
            {
                BucketName = _options.S3.BucketName,
                Key = normalizedKey
            };

            var response = await _s3Client.GetObjectAsync(getRequest, ct);
            var ext = Path.GetExtension(normalizedKey);
            var contentType = !string.IsNullOrWhiteSpace(response.Headers.ContentType)
                ? response.Headers.ContentType
                : MediaFileValidator.GetContentTypeForExtension(ext);

            long contentLength = response.ContentLength > 0 
                ? response.ContentLength 
                : (response.Headers?.ContentLength ?? (response.ResponseStream?.CanSeek == true ? response.ResponseStream.Length : 0));

            if (response.ResponseStream == null)
            {
                response.Dispose();
                return null;
            }

            return new StorageFileResult(
                response.ResponseStream,
                contentType,
                response.ETag,
                response.LastModified,
                contentLength,
                response
            );

        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve S3 object {Key} from bucket {Bucket}", normalizedKey, _options.S3.BucketName);
            return null;
        }
    }

    public string BuildPublicUrl(string storageKey)
    {
        var normalizedKey = storageKey.Replace('\\', '/').TrimStart('/');

        if (!string.IsNullOrWhiteSpace(_options.S3.PublicBaseUrl))
        {
            return $"{_options.S3.PublicBaseUrl.TrimEnd('/')}/{normalizedKey}";
        }

        if (!string.IsNullOrWhiteSpace(_options.S3.ServiceURL))
        {
            return $"{_options.S3.ServiceURL.TrimEnd('/')}/{_options.S3.BucketName}/{normalizedKey}";
        }

        var region = string.IsNullOrWhiteSpace(_options.S3.Region) ? "ap-southeast-1" : _options.S3.Region;
        return $"https://{_options.S3.BucketName}.s3.{region}.amazonaws.com/{normalizedKey}";
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _s3Client.Dispose();
        }
    }
}

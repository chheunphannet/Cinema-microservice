using System.Net;
using System.Security.Cryptography;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Catalog.Api.Endpoints;
using Catalog.Api.Models;
using Cinema.Foundation.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace Cinema.UnitTests;

public class Phase3StorageAndMediaTests : IDisposable
{
    private readonly string _testStorageDir;

    public Phase3StorageAndMediaTests()
    {
        _testStorageDir = Path.Combine(Path.GetTempPath(), "cinema_test_storage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testStorageDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testStorageDir))
            {
                Directory.Delete(_testStorageDir, recursive: true);
            }
        }
        catch { }
    }

    private LocalStorageService CreateLocalStorageService(string? basePublicUrl = null)
    {
        var options = new StorageOptions
        {
            Provider = "Local",
            Local = new LocalStorageOptions
            {
                StoragePath = _testStorageDir,
                BasePublicUrl = basePublicUrl ?? "/api/v1/media/files",
                SigningSecret = "test-secret-key-32-characters-long-cinema!"
            }
        };

        return new LocalStorageService(Options.Create(options));
    }

    #region 1. MediaFileValidator Tests

    [Theory]
    [InlineData("posters")]
    [InlineData("backdrops")]
    [InlineData("trailers")]
    [InlineData("screen-logos")]
    [InlineData("hall-logos")]
    [InlineData("promotions")]
    [InlineData("branch-gallery")]
    [InlineData("concessions")]
    [InlineData("POSTERS")]
    [InlineData("BackDrops")]
    public void MediaFileValidator_ValidateCategory_AllowsValidCategories(string category)
    {
        var (isValid, errorMessage) = MediaFileValidator.ValidateCategory(category);
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("malicious")]
    [InlineData("executables")]
    [InlineData("system")]
    public void MediaFileValidator_ValidateCategory_RejectsInvalidCategories(string? category)
    {
        var (isValid, errorMessage) = MediaFileValidator.ValidateCategory(category);
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
    }

    [Theory]
    [InlineData("poster.jpg", "image/jpeg")]
    [InlineData("poster.jpeg", "image/jpeg")]
    [InlineData("poster.png", "image/png")]
    [InlineData("poster.webp", "image/webp")]
    [InlineData("logo.svg", "image/svg+xml")]
    [InlineData("trailer.mp4", "video/mp4")]
    [InlineData("teaser.webm", "video/webm")]
    public void MediaFileValidator_ValidateExtensionAndContentType_AllowsValidCombinations(string fileName, string contentType)
    {
        var (isValid, errorMessage) = MediaFileValidator.ValidateExtensionAndContentType(fileName, contentType);
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData("malware.exe", "application/x-msdownload")]
    [InlineData("script.sh", "application/x-sh")]
    [InlineData("page.php", "application/x-php")]
    [InlineData("doc.pdf", "application/pdf")]
    [InlineData("archive.zip", "application/zip")]
    [InlineData("code.js", "application/javascript")]
    public void MediaFileValidator_ValidateExtensionAndContentType_RejectsDisallowedExtensions(string fileName, string contentType)
    {
        var (isValid, errorMessage) = MediaFileValidator.ValidateExtensionAndContentType(fileName, contentType);
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("not supported", errorMessage);
    }

    [Theory]
    [InlineData("poster.jpg", "image/png")]
    [InlineData("poster.png", "image/jpeg")]
    [InlineData("trailer.mp4", "image/png")]
    [InlineData("poster.webp", "video/mp4")]
    public void MediaFileValidator_ValidateExtensionAndContentType_RejectsMismatchedMime(string fileName, string contentType)
    {
        var (isValid, errorMessage) = MediaFileValidator.ValidateExtensionAndContentType(fileName, contentType);
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("does not match", errorMessage);
    }

    [Fact]
    public void MediaFileValidator_ValidateFileSize_EnforcesImageLimit10MB()
    {
        const long limit = MediaFileValidator.MaxImageSizeBytes; // 10MB
        var validResult = MediaFileValidator.ValidateFileSize(limit, ".jpg");
        Assert.True(validResult.IsValid);

        var exceedsResult = MediaFileValidator.ValidateFileSize(limit + 1, ".jpg");
        Assert.False(exceedsResult.IsValid);
        Assert.Contains("exceeds maximum limit of 10MB", exceedsResult.ErrorMessage);
    }

    [Fact]
    public void MediaFileValidator_ValidateFileSize_EnforcesVideoLimit100MB()
    {
        const long limit = MediaFileValidator.MaxVideoSizeBytes; // 100MB
        var validResult = MediaFileValidator.ValidateFileSize(limit, ".mp4");
        Assert.True(validResult.IsValid);

        var exceedsResult = MediaFileValidator.ValidateFileSize(limit + 1, ".mp4");
        Assert.False(exceedsResult.IsValid);
        Assert.Contains("exceeds maximum limit of 100MB", exceedsResult.ErrorMessage);
    }

    [Fact]
    public void MediaFileValidator_ValidateFileSize_RejectsEmptyFile()
    {
        var result = MediaFileValidator.ValidateFileSize(0, ".png");
        Assert.False(result.IsValid);
        Assert.Contains("empty", result.ErrorMessage);
    }

    [Fact]
    public void MediaFileValidator_ValidateMagicNumbers_ValidatesJpeg()
    {
        var validJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        using var validStream = new MemoryStream(validJpeg);
        var validResult = MediaFileValidator.ValidateMagicNumbers(validStream, ".jpg");
        Assert.True(validResult.IsValid);

        var invalidBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        using var invalidStream = new MemoryStream(invalidBytes);
        var invalidResult = MediaFileValidator.ValidateMagicNumbers(invalidStream, ".jpg");
        Assert.False(invalidResult.IsValid);
    }

    [Fact]
    public void MediaFileValidator_ValidateMagicNumbers_ValidatesPng()
    {
        var validPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        using var validStream = new MemoryStream(validPng);
        var validResult = MediaFileValidator.ValidateMagicNumbers(validStream, ".png");
        Assert.True(validResult.IsValid);

        var invalidBytes = new byte[] { 0x89, 0x50, 0x00, 0x00 };
        using var invalidStream = new MemoryStream(invalidBytes);
        var invalidResult = MediaFileValidator.ValidateMagicNumbers(invalidStream, ".png");
        Assert.False(invalidResult.IsValid);
    }

    [Fact]
    public void MediaFileValidator_ValidateMagicNumbers_ValidatesWebP()
    {
        var validWebp = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x20, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        using var validStream = new MemoryStream(validWebp);
        var validResult = MediaFileValidator.ValidateMagicNumbers(validStream, ".webp");
        Assert.True(validResult.IsValid);

        var invalidBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        using var invalidStream = new MemoryStream(invalidBytes);
        var invalidResult = MediaFileValidator.ValidateMagicNumbers(invalidStream, ".webp");
        Assert.False(invalidResult.IsValid);
    }

    [Fact]
    public void MediaFileValidator_ValidateMagicNumbers_ValidatesSvgAndRejectsScriptInjection()
    {
        var validSvg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\"><circle r=\"50\"/></svg>");
        using var validStream = new MemoryStream(validSvg);
        var validResult = MediaFileValidator.ValidateMagicNumbers(validStream, ".svg");
        Assert.True(validResult.IsValid);

        // SVG containing XSS injection should be rejected
        var xssSvg = Encoding.UTF8.GetBytes("<svg><script>alert('xss')</script></svg>");
        using var xssStream = new MemoryStream(xssSvg);
        var xssResult = MediaFileValidator.ValidateMagicNumbers(xssStream, ".svg");
        Assert.False(xssResult.IsValid);
        Assert.Contains("XSS prevention", xssResult.ErrorMessage);
    }

    [Fact]
    public void MediaFileValidator_ValidateMagicNumbers_ValidatesMp4()
    {
        var validMp4 = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32 };
        using var validStream = new MemoryStream(validMp4);
        var validResult = MediaFileValidator.ValidateMagicNumbers(validStream, ".mp4");
        Assert.True(validResult.IsValid);

        var invalidMp4 = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x61, 0x62, 0x63, 0x64 };
        using var invalidStream = new MemoryStream(invalidMp4);
        var invalidResult = MediaFileValidator.ValidateMagicNumbers(invalidStream, ".mp4");
        Assert.False(invalidResult.IsValid);
    }

    [Fact]
    public void MediaFileValidator_ValidateMagicNumbers_ValidatesWebM()
    {
        var validWebm = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81 };
        using var validStream = new MemoryStream(validWebm);
        var validResult = MediaFileValidator.ValidateMagicNumbers(validStream, ".webm");
        Assert.True(validResult.IsValid);

        var invalidBytes = new byte[] { 0x1A, 0x45, 0x00, 0x00 };
        using var invalidStream = new MemoryStream(invalidBytes);
        var invalidResult = MediaFileValidator.ValidateMagicNumbers(invalidStream, ".webm");
        Assert.False(invalidResult.IsValid);
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\win.ini", "win.ini")]
    [InlineData("folder/nested/image.png", "image.png")]
    [InlineData("normal_poster.jpg", "normal_poster.jpg")]
    [InlineData("bad:?*<>|name.png", "badname.png")]
    public void MediaFileValidator_SanitizeFileName_StripsDangerousCharacters(string input, string expected)
    {
        var sanitized = MediaFileValidator.SanitizeFileName(input);
        Assert.Equal(expected, sanitized);
    }

    #endregion

    #region 2. LocalStorageService Operations Tests

    [Fact]
    public async Task LocalStorageService_UploadAsync_SavesFileToDiskAndReturnsResult()
    {
        var service = CreateLocalStorageService();
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x11, 0x22, 0x33, 0x44 };
        using var stream = new MemoryStream(content);

        var result = await service.UploadAsync(stream, "my-poster.jpg", "image/jpeg", "posters");

        Assert.NotNull(result);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(content.Length, result.SizeBytes);
        Assert.StartsWith("/api/v1/media/files/posters/", result.PublicUrl);
        Assert.StartsWith("posters/", result.StorageKey);

        // Verify file exists on disk
        var diskPath = Path.Combine(_testStorageDir, result.StorageKey);
        Assert.True(File.Exists(diskPath));
        Assert.Equal(content.Length, new FileInfo(diskPath).Length);
    }

    [Fact]
    public async Task LocalStorageService_UploadAsync_GeneratesUniqueKeysForSameFilename()
    {
        var service = CreateLocalStorageService();
        using var stream1 = new MemoryStream(new byte[] { 1, 2, 3 });
        using var stream2 = new MemoryStream(new byte[] { 4, 5, 6 });

        var result1 = await service.UploadAsync(stream1, "banner.png", "image/png", "promotions");
        var result2 = await service.UploadAsync(stream2, "banner.png", "image/png", "promotions");

        Assert.NotEqual(result1.StorageKey, result2.StorageKey);
        Assert.NotEqual(result1.PublicUrl, result2.PublicUrl);
    }

    [Fact]
    public async Task LocalStorageService_GetFileAsync_RetrievesStoredFileAndMetadata()
    {
        var service = CreateLocalStorageService();
        var content = new byte[] { 10, 20, 30, 40, 50 };
        using var stream = new MemoryStream(content);

        var upload = await service.UploadAsync(stream, "test.png", "image/png", "screen-logos");

        await using var fileResult = await service.GetFileAsync(upload.StorageKey);

        Assert.NotNull(fileResult);
        Assert.Equal("image/png", fileResult.ContentType);
        Assert.Equal(content.Length, fileResult.SizeBytes);
        Assert.NotNull(fileResult.ETag);

        using var memory = new MemoryStream();
        await fileResult.ContentStream.CopyToAsync(memory);
        Assert.Equal(content, memory.ToArray());
    }

    [Fact]
    public async Task LocalStorageService_GetFileAsync_ReturnsNullForNonExistentFile()
    {
        var service = CreateLocalStorageService();
        var result = await service.GetFileAsync("posters/non_existent_file.png");
        Assert.Null(result);
    }

    [Fact]
    public async Task LocalStorageService_GetFileAsync_BlocksPathTraversal()
    {
        var service = CreateLocalStorageService();
        var result = await service.GetFileAsync("../../windows/win.ini");
        Assert.Null(result);
    }

    [Fact]
    public async Task LocalStorageService_DeleteAsync_RemovesExistingFile()
    {
        var service = CreateLocalStorageService();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var upload = await service.UploadAsync(stream, "to_delete.webp", "image/webp", "concessions");

        var deleted = await service.DeleteAsync(upload.StorageKey);
        Assert.True(deleted);

        // Verify it is gone
        var existsAgain = await service.GetFileAsync(upload.StorageKey);
        Assert.Null(existsAgain);
    }

    [Fact]
    public async Task LocalStorageService_DeleteAsync_ReturnsFalseForNonExistentFile()
    {
        var service = CreateLocalStorageService();
        var deleted = await service.DeleteAsync("concessions/not_there.png");
        Assert.False(deleted);
    }

    [Fact]
    public async Task LocalStorageService_PresignedUrl_GeneratesValidSignedUrlAndVerifies()
    {
        var service = CreateLocalStorageService();
        var expiresIn = TimeSpan.FromMinutes(10);

        var presigned = await service.GeneratePresignedUploadUrlAsync("movie_banner.jpg", "image/jpeg", "backdrops", expiresIn);

        Assert.NotNull(presigned);
        Assert.Contains("sig=", presigned.UploadUrl);
        Assert.Contains("exp=", presigned.UploadUrl);
        Assert.StartsWith("/api/v1/media/files/backdrops/", presigned.PublicUrl);

        // Parse sig and exp from query string
        var uri = new Uri("http://localhost" + presigned.UploadUrl);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        var sig = query["sig"].ToString();
        var exp = long.Parse(query["exp"].ToString());

        // Validate signature succeeds
        Assert.True(service.ValidatePresignedSignature(presigned.StorageKey, exp, sig));

        // Tampered signature fails
        var tamperedSig = sig.EndsWith('a') ? sig[..^1] + "b" : sig[..^1] + "a";
        Assert.False(service.ValidatePresignedSignature(presigned.StorageKey, exp, tamperedSig));

        // Expired timestamp fails
        var expiredTime = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        Assert.False(service.ValidatePresignedSignature(presigned.StorageKey, expiredTime, sig));
    }

    #endregion

    #region 3. S3StorageService Operations Tests

    [Fact]
    public void S3StorageService_BuildPublicUrl_FormatsDefaultAwsUrl()
    {
        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions
            {
                BucketName = "my-cinema-bucket",
                Region = "ap-southeast-1"
            }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: new Mock<IAmazonS3>().Object);
        var url = service.BuildPublicUrl("posters/avatar.jpg");

        Assert.Equal("https://my-cinema-bucket.s3.ap-southeast-1.amazonaws.com/posters/avatar.jpg", url);
    }

    [Fact]
    public void S3StorageService_BuildPublicUrl_FormatsMinIoServiceUrl()
    {
        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions
            {
                ServiceURL = "http://minio:9000",
                BucketName = "cinema-media"
            }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: new Mock<IAmazonS3>().Object);
        var url = service.BuildPublicUrl("trailers/teaser.mp4");

        Assert.Equal("http://minio:9000/cinema-media/trailers/teaser.mp4", url);
    }

    [Fact]
    public void S3StorageService_BuildPublicUrl_FormatsCustomCdnPublicBaseUrl()
    {
        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions
            {
                PublicBaseUrl = "https://cdn.cinemacity.kh",
                BucketName = "cinema-media"
            }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: new Mock<IAmazonS3>().Object);
        var url = service.BuildPublicUrl("promotions/summer.webp");

        Assert.Equal("https://cdn.cinemacity.kh/promotions/summer.webp", url);
    }

    [Fact]
    public async Task S3StorageService_UploadAsync_CallsS3PutObject()
    {
        var mockS3 = new Mock<IAmazonS3>();
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new PutObjectResponse());

        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions { BucketName = "cinema-bucket", Region = "ap-southeast-1" }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: mockS3.Object);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await service.UploadAsync(stream, "poster.jpg", "image/jpeg", "posters");

        Assert.NotNull(result);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.StartsWith("https://cinema-bucket.s3.ap-southeast-1.amazonaws.com/posters/", result.PublicUrl);

        mockS3.Verify(x => x.PutObjectAsync(It.Is<PutObjectRequest>(r => r.BucketName == "cinema-bucket" && r.ContentType == "image/jpeg"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task S3StorageService_GeneratePresignedUploadUrlAsync_CallsGetPreSignedURLAsync()
    {
        var mockS3 = new Mock<IAmazonS3>();
        mockS3.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
              .ReturnsAsync("https://s3.amazonaws.com/cinema-bucket/key?signed=true");

        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions { BucketName = "cinema-bucket", Region = "ap-southeast-1" }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: mockS3.Object);
        var result = await service.GeneratePresignedUploadUrlAsync("movie.mp4", "video/mp4", "trailers", TimeSpan.FromMinutes(15));

        Assert.NotNull(result);
        Assert.Equal("https://s3.amazonaws.com/cinema-bucket/key?signed=true", result.UploadUrl);
        Assert.StartsWith("https://cinema-bucket.s3.ap-southeast-1.amazonaws.com/trailers/", result.PublicUrl);
    }

    [Fact]
    public async Task S3StorageService_DeleteAsync_ReturnsTrueOnSuccess()
    {
        var mockS3 = new Mock<IAmazonS3>();
        mockS3.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new DeleteObjectResponse());

        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions { BucketName = "cinema-bucket" }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: mockS3.Object);
        var deleted = await service.DeleteAsync("posters/test.jpg");

        Assert.True(deleted);
        mockS3.Verify(x => x.DeleteObjectAsync(It.Is<DeleteObjectRequest>(r => r.BucketName == "cinema-bucket" && r.Key == "posters/test.jpg"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task S3StorageService_DeleteAsync_HandlesNotFoundGracefully()
    {
        var mockS3 = new Mock<IAmazonS3>();
        mockS3.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions { BucketName = "cinema-bucket" }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: mockS3.Object);
        var deleted = await service.DeleteAsync("posters/not_found.jpg");

        Assert.False(deleted);
    }

    #endregion

    #region 4. Dependency Injection Extensions Tests

    [Fact]
    public void StorageServiceExtensions_AddCinemaStorage_RegistersLocalStorageByDefault()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Storage:Provider", "Local" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddCinemaStorage(config);
        using var provider = services.BuildServiceProvider();

        var storage = provider.GetService<IStorageService>();
        Assert.NotNull(storage);
        Assert.IsType<LocalStorageService>(storage);
        Assert.Equal("Local", storage.ProviderName);
    }

    [Fact]
    public void StorageServiceExtensions_AddCinemaStorage_RegistersS3StorageWhenConfigured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Storage:Provider", "S3" },
                { "Storage:S3:BucketName", "test-bucket" },
                { "Storage:S3:Region", "ap-southeast-1" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddCinemaStorage(config);
        using var provider = services.BuildServiceProvider();

        var storage = provider.GetService<IStorageService>();
        Assert.NotNull(storage);
        Assert.IsType<S3StorageService>(storage);
        Assert.Equal("S3", storage.ProviderName);
    }

    [Fact]
    public void StorageServiceExtensions_AddCinemaStorage_FallsBackToLocalWhenProviderEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddCinemaStorage(config);
        using var provider = services.BuildServiceProvider();

        var storage = provider.GetService<IStorageService>();
        Assert.NotNull(storage);
        Assert.IsType<LocalStorageService>(storage);
    }

    #endregion

    #region 5. Media Endpoints Request Validation & Handlers Tests

    [Fact]
    public async Task MediaEndpoints_HandlePresignUploadAsync_ValidRequest_ReturnsOk()
    {
        var storageMock = new Mock<IStorageService>();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        storageMock.Setup(s => s.GeneratePresignedUploadUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new PresignedUploadResult("http://upload-url", "http://public-url", "posters/123_poster.jpg", expiresAt));

        var request = new PresignUploadRequest("poster.jpg", "image/jpeg", "posters");

        var result = await MediaEndpoints.HandlePresignUploadAsync(request, storageMock.Object, CancellationToken.None);

        Assert.NotNull(result);
        var okResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var response = Assert.IsType<PresignedUploadResponse>(okResult.Value);
        Assert.Equal("http://upload-url", response.UploadUrl);
        Assert.Equal("http://public-url", response.PublicUrl);
        Assert.Equal("posters/123_poster.jpg", response.StorageKey);
    }

    [Fact]
    public async Task MediaEndpoints_HandlePresignUploadAsync_InvalidCategory_ReturnsBadRequest()
    {
        var storageMock = new Mock<IStorageService>();
        var request = new PresignUploadRequest("poster.jpg", "image/jpeg", "invalid-category");

        var result = await MediaEndpoints.HandlePresignUploadAsync(request, storageMock.Object, CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        Assert.Contains("Invalid category", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public async Task MediaEndpoints_HandlePresignUploadAsync_DisallowedExtension_ReturnsBadRequest()
    {
        var storageMock = new Mock<IStorageService>();
        var request = new PresignUploadRequest("malware.exe", "application/x-msdownload", "posters");

        var result = await MediaEndpoints.HandlePresignUploadAsync(request, storageMock.Object, CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        Assert.Contains("not supported", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public async Task MediaEndpoints_HandlePresignUploadAsync_MismatchedMime_ReturnsBadRequest()
    {
        var storageMock = new Mock<IStorageService>();
        var request = new PresignUploadRequest("poster.png", "image/jpeg", "posters");

        var result = await MediaEndpoints.HandlePresignUploadAsync(request, storageMock.Object, CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        Assert.Contains("does not match", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public async Task MediaEndpoints_HandleDeleteAsync_ExistingFile_ReturnsOk()
    {
        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.DeleteAsync("posters/avatar.jpg", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var result = await MediaEndpoints.HandleDeleteAsync("posters", "avatar.jpg", storageMock.Object, CancellationToken.None);

        Assert.NotNull(result);
        var okResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task MediaEndpoints_HandleDeleteAsync_NonExistentFile_ReturnsNotFound()
    {
        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var result = await MediaEndpoints.HandleDeleteAsync("posters", "missing.jpg", storageMock.Object, CancellationToken.None);

        var notFoundResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task MediaEndpoints_HandleGetFileAsync_NonExistentFile_ReturnsNotFound()
    {
        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.GetFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((StorageFileResult?)null);

        var context = new DefaultHttpContext();
        var result = await MediaEndpoints.HandleGetFileAsync("posters", "missing.jpg", context, storageMock.Object, CancellationToken.None);

        var notFoundResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task MediaEndpoints_HandleGetFileAsync_ExistingFile_ReturnsStreamAndCacheHeaders()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileResult = new StorageFileResult(stream, "image/jpeg", "\"etag123\"", DateTimeOffset.UtcNow, 3);

        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.GetFileAsync("posters/valid.jpg", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(fileResult);

        var context = new DefaultHttpContext();
        var result = await MediaEndpoints.HandleGetFileAsync("posters", "valid.jpg", context, storageMock.Object, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("\"etag123\"", context.Response.Headers.ETag.ToString());
        Assert.Contains("public", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task MediaEndpoints_HandleGetFileAsync_ConditionalGet_Returns304NotModified()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileResult = new StorageFileResult(stream, "image/jpeg", "\"etag123\"", DateTimeOffset.UtcNow, 3);

        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.GetFileAsync("posters/valid.jpg", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(fileResult);

        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = "\"etag123\"";

        var result = await MediaEndpoints.HandleGetFileAsync("posters", "valid.jpg", context, storageMock.Object, CancellationToken.None);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
    }

    [Fact]
    public async Task MediaEndpoints_HandleUploadAsync_NonMultipart_ReturnsBadRequest()
    {
        var storageMock = new Mock<IStorageService>();
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";

        var result = await MediaEndpoints.HandleUploadAsync(context, storageMock.Object, CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        Assert.Contains("multipart/form-data", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public async Task MediaEndpoints_HandleUploadAsync_ValidFile_Returns200Ok()
    {
        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new StorageResult("/api/v1/media/files/posters/avatar.jpg", "posters/avatar.jpg", "image/jpeg", 8));

        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        var formFile = new FormFile(new MemoryStream(jpegBytes), 0, jpegBytes.Length, "file", "avatar.jpg")
        {
            Headers = new HeaderDictionary
            {
                { "Content-Type", "image/jpeg" }
            }
        };

        var form = new FormCollection(
            new Dictionary<string, StringValues> { { "category", "posters" } },
            new FormFileCollection { formFile });

        var context = new DefaultHttpContext();
        context.Request.ContentType = "multipart/form-data; boundary=---boundary";
        context.Request.Form = form;

        var result = await MediaEndpoints.HandleUploadAsync(context, storageMock.Object, CancellationToken.None);

        Assert.NotNull(result);
        var okResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var response = Assert.IsType<MediaUploadResponse>(okResult.Value);
        Assert.Equal("posters/avatar.jpg", response.StorageKey);
        Assert.Equal("posters", response.Category);
        Assert.Equal("image/jpeg", response.ContentType);
    }

    [Fact]
    public async Task MediaEndpoints_HandleUploadAsync_EmptyFile_ReturnsBadRequest()
    {
        var storageMock = new Mock<IStorageService>();
        var formFile = new FormFile(new MemoryStream(Array.Empty<byte>()), 0, 0, "file", "empty.jpg");
        var form = new FormCollection(
            new Dictionary<string, StringValues> { { "category", "posters" } },
            new FormFileCollection { formFile });

        var context = new DefaultHttpContext();
        context.Request.ContentType = "multipart/form-data; boundary=---boundary";
        context.Request.Form = form;

        var result = await MediaEndpoints.HandleUploadAsync(context, storageMock.Object, CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        Assert.Contains("non-empty", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public async Task MediaEndpoints_HandleUploadAsync_MissingCategory_ReturnsBadRequest()
    {
        var storageMock = new Mock<IStorageService>();
        var formFile = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "file", "poster.jpg");
        var form = new FormCollection(
            new Dictionary<string, StringValues>(),
            new FormFileCollection { formFile });

        var context = new DefaultHttpContext();
        context.Request.ContentType = "multipart/form-data; boundary=---boundary";
        context.Request.Form = form;

        var result = await MediaEndpoints.HandleUploadAsync(context, storageMock.Object, CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        Assert.Contains("Category is required", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public async Task MediaEndpoints_HandlePutPresignedFileAsync_ValidSignature_SavesFile()
    {
        var service = CreateLocalStorageService();
        var presigned = await service.GeneratePresignedUploadUrlAsync("movie.jpg", "image/jpeg", "posters", TimeSpan.FromMinutes(10));

        var uri = new Uri("http://localhost" + presigned.UploadUrl);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(uri.Query);
        var fileBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        context.Request.Body = new MemoryStream(fileBytes);
        context.Request.ContentType = "image/jpeg";

        var parts = presigned.StorageKey.Split('/');
        var category = parts[0];
        var fileName = parts[1];

        var result = await MediaEndpoints.HandlePutPresignedFileAsync(category, fileName, context, service, CancellationToken.None);

        Assert.NotNull(result);
        var okResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        var response = Assert.IsType<MediaUploadResponse>(okResult.Value);
        Assert.Equal(presigned.StorageKey, response.StorageKey);

        var diskFile = Path.Combine(_testStorageDir, presigned.StorageKey);
        Assert.True(File.Exists(diskFile));
        Assert.Equal(fileBytes.Length, new FileInfo(diskFile).Length);
    }

    [Fact]
    public async Task MediaEndpoints_HandlePutPresignedFileAsync_InvalidSignature_ReturnsForbidden()
    {
        var service = CreateLocalStorageService();
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?sig=bad-signature&exp=9999999999");
        context.Request.Body = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await MediaEndpoints.HandlePutPresignedFileAsync("posters", "avatar.jpg", context, service, CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problemResult.StatusCode);
    }

    [Fact]
    public async Task S3StorageService_GetFileAsync_RetrievesObjectAndMetadata()
    {
        var mockS3 = new Mock<IAmazonS3>();
        var content = new byte[] { 1, 2, 3, 4 };
        var getResponse = new GetObjectResponse
        {
            ResponseStream = new MemoryStream(content),
            ETag = "\"s3-etag\"",
            LastModified = DateTime.UtcNow,
            ContentLength = content.Length
        };
        getResponse.Headers.ContentType = "image/jpeg";
        getResponse.Headers.ContentLength = content.Length;


        mockS3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(getResponse);

        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions { BucketName = "cinema-bucket" }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: mockS3.Object);
        await using var file = await service.GetFileAsync("posters/test.jpg");

        Assert.NotNull(file);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Equal("\"s3-etag\"", file.ETag);
        Assert.Equal(content.Length, file.SizeBytes);
    }

    [Fact]
    public async Task S3StorageService_GetFileAsync_NotFound_ReturnsNull()
    {
        var mockS3 = new Mock<IAmazonS3>();
        mockS3.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = HttpStatusCode.NotFound });

        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions { BucketName = "cinema-bucket" }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: mockS3.Object);
        var file = await service.GetFileAsync("posters/missing.jpg");

        Assert.Null(file);
    }

    [Fact]
    public void MediaFileValidator_ValidateMagicNumbers_ValidatesSvgWithXmlDeclarationAndCatchesLateScript()
    {
        // Valid SVG with XML declaration and comments
        var validXmlSvg = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!-- Cinema City SVG logo -->\n<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\"><circle r=\"50\"/></svg>";
        using var validStream = new MemoryStream(Encoding.UTF8.GetBytes(validXmlSvg));
        var validResult = MediaFileValidator.ValidateMagicNumbers(validStream, ".svg");
        Assert.True(validResult.IsValid);

        // Malicious SVG where <script> is placed well past byte 32
        var lateScriptSvg = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\">\n<script>alert('xss')</script></svg>";
        using var lateStream = new MemoryStream(Encoding.UTF8.GetBytes(lateScriptSvg));
        var lateResult = MediaFileValidator.ValidateMagicNumbers(lateStream, ".svg");
        Assert.False(lateResult.IsValid);
        Assert.Contains("XSS prevention", lateResult.ErrorMessage);
    }

    [Fact]
    public void MediaFileValidator_ValidateMagicNumbers_RejectsSvgWithOnloadHandler()
    {
        var onloadSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"><circle r=\"10\"/></svg>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(onloadSvg));
        var result = MediaFileValidator.ValidateMagicNumbers(stream, ".svg");
        Assert.False(result.IsValid);
        Assert.Contains("XSS prevention", result.ErrorMessage);
    }

    [Fact]
    public void MediaFileValidator_ValidateMagicNumbers_RejectsTruncatedWebP()
    {
        // Only 8 bytes (RIFF header without WEBP atom)
        var truncatedWebp = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(truncatedWebp);
        var result = MediaFileValidator.ValidateMagicNumbers(stream, ".webp");
        Assert.False(result.IsValid);
        Assert.Contains("WebP", result.ErrorMessage);
    }

    [Fact]
    public async Task LocalStorageService_GetFileAsync_BlocksSiblingDirectoryTraversal()
    {
        var service = CreateLocalStorageService();
        // Sibling path attempt (e.g. storage_evil)
        var result = await service.GetFileAsync("../" + Path.GetFileName(_testStorageDir) + "_evil/secret.txt");
        Assert.Null(result);
    }

    [Fact]
    public async Task MediaEndpoints_HandlePutPresignedFileAsync_EmptyFile_ReturnsBadRequest()
    {
        var service = CreateLocalStorageService();
        var presigned = await service.GeneratePresignedUploadUrlAsync("empty.jpg", "image/jpeg", "posters", TimeSpan.FromMinutes(10));
        var uri = new Uri("http://localhost" + presigned.UploadUrl);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(uri.Query);
        context.Request.Body = new MemoryStream(Array.Empty<byte>());
        context.Request.ContentType = "image/jpeg";

        var parts = presigned.StorageKey.Split('/');
        var result = await MediaEndpoints.HandlePutPresignedFileAsync(parts[0], parts[1], context, service, CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        Assert.Contains("empty", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public async Task MediaEndpoints_HandlePutPresignedFileAsync_ExceedsSizeLimit_ReturnsBadRequest()
    {
        var service = CreateLocalStorageService();
        var presigned = await service.GeneratePresignedUploadUrlAsync("huge.jpg", "image/jpeg", "posters", TimeSpan.FromMinutes(10));
        var uri = new Uri("http://localhost" + presigned.UploadUrl);

        // 11MB file (exceeds 10MB limit)
        var hugeData = new byte[11 * 1024 * 1024];
        hugeData[0] = 0xFF; hugeData[1] = 0xD8; hugeData[2] = 0xFF; hugeData[3] = 0xE0;

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(uri.Query);
        context.Request.Body = new MemoryStream(hugeData);
        context.Request.ContentType = "image/jpeg";

        var parts = presigned.StorageKey.Split('/');
        var result = await MediaEndpoints.HandlePutPresignedFileAsync(parts[0], parts[1], context, service, CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        Assert.Contains("exceeds maximum limit", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public async Task MediaEndpoints_HandlePutPresignedFileAsync_InvalidMagicNumbers_ReturnsBadRequest()
    {
        var service = CreateLocalStorageService();
        var presigned = await service.GeneratePresignedUploadUrlAsync("fake.jpg", "image/jpeg", "posters", TimeSpan.FromMinutes(10));
        var uri = new Uri("http://localhost" + presigned.UploadUrl);

        // Disguised exe bytes
        var fakeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(uri.Query);
        context.Request.Body = new MemoryStream(fakeBytes);
        context.Request.ContentType = "image/jpeg";

        var parts = presigned.StorageKey.Split('/');
        var result = await MediaEndpoints.HandlePutPresignedFileAsync(parts[0], parts[1], context, service, CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        Assert.Contains("header signature does not match", problemResult.ProblemDetails.Detail);
    }

    [Fact]
    public void StorageFileResult_Dispose_DisposesParentResource()
    {
        var mockDisposable = new Mock<IDisposable>();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var fileResult = new StorageFileResult(stream, "image/png", "\"etag\"", DateTimeOffset.UtcNow, 3, mockDisposable.Object);

        fileResult.Dispose();

        mockDisposable.Verify(d => d.Dispose(), Times.Once);
    }

    [Fact]
    public async Task MediaEndpoints_HandleGetFileAsync_Svg_SetsContentSecurityPolicyAndNosniff()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("<svg></svg>"));
        var fileResult = new StorageFileResult(stream, "image/svg+xml", "\"etag-svg\"", DateTimeOffset.UtcNow, 11);

        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.GetFileAsync("screen-logos/logo.svg", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(fileResult);

        var context = new DefaultHttpContext();
        var result = await MediaEndpoints.HandleGetFileAsync("screen-logos", "logo.svg", context, storageMock.Object, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Contains("sandbox", context.Response.Headers["Content-Security-Policy"].ToString());
    }

    [Fact]
    public void S3StorageService_MinIoConfiguration_FormatsProxiedPublicBaseUrl()
    {
        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions
            {
                ServiceURL = "http://minio:9000",
                BucketName = "cinema-media",
                ForcePathStyle = true,
                AccessKey = "minioadmin",
                SecretKey = "change-this-development-password",
                PublicBaseUrl = "http://localhost:8080/api/v1/media/files"
            }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: new Mock<IAmazonS3>().Object);
        var url = service.BuildPublicUrl("posters/oppenheimer.jpg");

        Assert.Equal("http://localhost:8080/api/v1/media/files/posters/oppenheimer.jpg", url);
    }

    [Fact]
    public void S3StorageService_MinIoConfiguration_WithoutPublicBaseUrl_FormatsMinIoDirectUrl()
    {
        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions
            {
                ServiceURL = "http://minio:9000",
                BucketName = "cinema-media",
                ForcePathStyle = true,
                AccessKey = "minioadmin",
                SecretKey = "change-this-development-password",
                PublicBaseUrl = ""
            }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: new Mock<IAmazonS3>().Object);
        var url = service.BuildPublicUrl("trailers/dune2.mp4");

        Assert.Equal("http://minio:9000/cinema-media/trailers/dune2.mp4", url);
    }

    [Fact]
    public void StorageServiceExtensions_AddCinemaStorage_WithMinIoSettings_RegistersS3StorageService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Storage:Provider", "S3" },
                { "Storage:S3:BucketName", "cinema-media" },
                { "Storage:S3:ServiceURL", "http://minio:9000" },
                { "Storage:S3:ForcePathStyle", "true" },
                { "Storage:S3:AccessKey", "minioadmin" },
                { "Storage:S3:SecretKey", "change-this-development-password" },
                { "Storage:S3:PublicBaseUrl", "http://localhost:8080/api/v1/media/files" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddCinemaStorage(config);
        using var provider = services.BuildServiceProvider();

        var storage = provider.GetService<IStorageService>();
        Assert.NotNull(storage);
        Assert.IsType<S3StorageService>(storage);
        Assert.Equal("S3", storage.ProviderName);

        var s3Service = (S3StorageService)storage;
        var publicUrl = s3Service.BuildPublicUrl("posters/interstellar.png");
        Assert.Equal("http://localhost:8080/api/v1/media/files/posters/interstellar.png", publicUrl);
    }

    [Fact]
    public void HtmlPortal_ContainsMinIoConsoleLinksAndCredentials()
    {
        var html = Gateway.Api.HtmlPortal.Content;
        Assert.Contains("MinIO S3 Console", html);
        Assert.Contains("/minio-console", html);
        Assert.Contains("9001", html);
        Assert.Contains("cinema-media", html);
        Assert.Contains("minioadmin", html);
    }

    [Fact]
    public async Task MinIoStorage_S3ApiCompatibility_GeneratesValidS3Requests()
    {
        var mockS3 = new Mock<IAmazonS3>();
        var putCalled = false;
        mockS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
              .Callback<PutObjectRequest, CancellationToken>((req, _) =>
              {
                  Assert.Equal("cinema-media", req.BucketName);
                  Assert.StartsWith("posters/", req.Key);
                  Assert.Equal("image/png", req.ContentType);
                  putCalled = true;
              })
              .ReturnsAsync(new PutObjectResponse());

        var options = new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions
            {
                ServiceURL = "http://minio:9000",
                BucketName = "cinema-media",
                ForcePathStyle = true,
                AccessKey = "minioadmin",
                SecretKey = "change-this-development-password",
                PublicBaseUrl = "http://localhost:8080/api/v1/media/files"
            }
        };

        var service = new S3StorageService(Options.Create(options), s3Client: mockS3.Object);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await service.UploadAsync(stream, "sample.png", "image/png", "posters");

        Assert.True(putCalled);
        Assert.StartsWith("http://localhost:8080/api/v1/media/files/posters/", result.PublicUrl);
    }

    [Fact]
    public void GatewayProxyConfig_MinIoConsoleRoutes_AreConfiguredCorrectly()
    {
        var routes = Gateway.Api.GatewayProxyConfig.GetRoutes();

        var exactRoute = routes.FirstOrDefault(r => r.RouteId == "minio-console-exact-route");
        Assert.NotNull(exactRoute);
        Assert.Equal("minio-console-cluster", exactRoute.ClusterId);
        Assert.Equal("anonymous", exactRoute.AuthorizationPolicy);
        Assert.Equal("/minio-console", exactRoute.Match.Path);
        Assert.NotNull(exactRoute.Transforms);
        Assert.Contains(exactRoute.Transforms, t => t.ContainsKey("PathSet") && t["PathSet"] == "/");

        var catchAllRoute = routes.FirstOrDefault(r => r.RouteId == "minio-console-route");
        Assert.NotNull(catchAllRoute);
        Assert.Equal("minio-console-cluster", catchAllRoute.ClusterId);
        Assert.Equal("anonymous", catchAllRoute.AuthorizationPolicy);
        Assert.Equal("/minio-console/{**catch-all}", catchAllRoute.Match.Path);
        Assert.NotNull(catchAllRoute.Transforms);
        Assert.Contains(catchAllRoute.Transforms, t => t.ContainsKey("PathRemovePrefix") && t["PathRemovePrefix"] == "/minio-console");
    }

    [Fact]
    public void GatewayProxyConfig_MinIoS3Routes_AreConfiguredCorrectly()
    {
        var routes = Gateway.Api.GatewayProxyConfig.GetRoutes();

        var exactRoute = routes.FirstOrDefault(r => r.RouteId == "minio-s3-exact-route");
        Assert.NotNull(exactRoute);
        Assert.Equal("minio-s3-cluster", exactRoute.ClusterId);
        Assert.Equal("anonymous", exactRoute.AuthorizationPolicy);
        Assert.Equal("/s3", exactRoute.Match.Path);
        Assert.NotNull(exactRoute.Transforms);
        Assert.Contains(exactRoute.Transforms, t => t.ContainsKey("PathSet") && t["PathSet"] == "/");

        var catchAllRoute = routes.FirstOrDefault(r => r.RouteId == "minio-s3-route");
        Assert.NotNull(catchAllRoute);
        Assert.Equal("minio-s3-cluster", catchAllRoute.ClusterId);
        Assert.Equal("anonymous", catchAllRoute.AuthorizationPolicy);
        Assert.Equal("/s3/{**catch-all}", catchAllRoute.Match.Path);
        Assert.NotNull(catchAllRoute.Transforms);
        Assert.Contains(catchAllRoute.Transforms, t => t.ContainsKey("PathRemovePrefix") && t["PathRemovePrefix"] == "/s3");
    }

    [Fact]
    public void GatewayProxyConfig_Clusters_ConfiguresMinIoConsoleAndS3Destinations()
    {
        var clusters = Gateway.Api.GatewayProxyConfig.GetClusters();

        var consoleCluster = clusters.FirstOrDefault(c => c.ClusterId == "minio-console-cluster");
        Assert.NotNull(consoleCluster);
        Assert.NotNull(consoleCluster.Destinations);
        Assert.True(consoleCluster.Destinations.ContainsKey("d1"));
        Assert.Equal("http://minio:9001", consoleCluster.Destinations["d1"].Address);

        var s3Cluster = clusters.FirstOrDefault(c => c.ClusterId == "minio-s3-cluster");
        Assert.NotNull(s3Cluster);
        Assert.NotNull(s3Cluster.Destinations);
        Assert.True(s3Cluster.Destinations.ContainsKey("d1"));
        Assert.Equal("http://minio:9000", s3Cluster.Destinations["d1"].Address);
    }

    [Fact]
    public void GatewayProxyConfig_Clusters_WithConfigurationOverrides_UpdatesDestinations()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Services:MinioConsoleUrl", "http://custom-minio:9001" },
                { "Services:MinioS3Url", "http://custom-minio:9000" }
            })
            .Build();

        var clusters = Gateway.Api.GatewayProxyConfig.GetClusters(config);

        var consoleCluster = clusters.FirstOrDefault(c => c.ClusterId == "minio-console-cluster");
        Assert.NotNull(consoleCluster);
        Assert.Equal("http://custom-minio:9001", consoleCluster.Destinations!["d1"].Address);

        var s3Cluster = clusters.FirstOrDefault(c => c.ClusterId == "minio-s3-cluster");
        Assert.NotNull(s3Cluster);
        Assert.Equal("http://custom-minio:9000", s3Cluster.Destinations!["d1"].Address);
    }

    #endregion
}


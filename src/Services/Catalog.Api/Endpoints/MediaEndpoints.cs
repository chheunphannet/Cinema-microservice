using Catalog.Api.Models;
using Cinema.Foundation.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Api.Endpoints;

public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this IEndpointRouteBuilder routes)
    {
        var media = routes.MapGroup("/api/v1/media")
            .WithTags("Media Management");

        // 1. POST /api/v1/media/upload
        media.MapPost("/upload", HandleUploadAsync)
            .DisableAntiforgery()
            .RequireAuthorization("Supervisor")
            .WithSummary("Upload Media Asset")
            .WithDescription("Accepts multipart/form-data file and category. Validates MIME type, extension, magic numbers, and size limits (10MB image, 100MB video).")
            .Produces<MediaUploadResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // 2. POST /api/v1/media/presign-upload
        media.MapPost("/presign-upload", HandlePresignUploadAsync)
            .RequireAuthorization("Supervisor")
            .WithSummary("Generate Presigned Upload URL")
            .WithDescription("Generates a presigned PUT upload URL for direct S3/MinIO or LocalStorage upload.")
            .Produces<PresignedUploadResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // 3. GET /api/v1/media/files/{category}/{fileName}
        media.MapGet("/files/{category}/{fileName}", HandleGetFileAsync)
            .AllowAnonymous()
            .WithSummary("Serve Stored Media Asset")
            .WithDescription("Streams media files with ETag, Cache-Control, and Last-Modified headers for high-performance CDN/browser caching.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // 4. PUT /api/v1/media/files/{category}/{fileName} (for LocalStorage presigned direct PUT upload)
        media.MapPut("/files/{category}/{fileName}", HandlePutPresignedFileAsync)
            .DisableAntiforgery()
            .AllowAnonymous()
            .WithSummary("Direct Presigned PUT Upload")
            .WithDescription("Direct binary PUT upload endpoint supporting local development presigned URL uploads with HMAC signature verification.")
            .Produces<MediaUploadResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // 5. DELETE /api/v1/media/{category}/{fileName}
        media.MapDelete("/{category}/{fileName}", HandleDeleteAsync)
            .RequireAuthorization("Supervisor")
            .WithSummary("Delete Stored Media Asset")
            .WithDescription("Privileged administrative endpoint to remove media assets by category and file name.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    public static async Task<IResult> HandleUploadAsync(
        HttpContext context, 
        IStorageService storageService, 
        CancellationToken ct)
    {
        if (!context.Request.HasFormContentType)
        {
            return Results.Problem(
                detail: "Request must be multipart/form-data.", 
                statusCode: StatusCodes.Status400BadRequest, 
                title: "Invalid Content-Type");
        }

        var form = await context.Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? (form.Files.Count > 0 ? form.Files[0] : null);

        if (file == null || file.Length == 0)
        {
            return Results.Problem(
                detail: "A valid, non-empty file must be uploaded under form field 'file'.", 
                statusCode: StatusCodes.Status400BadRequest, 
                title: "Missing File");
        }

        var category = form["category"].FirstOrDefault() ?? context.Request.Query["category"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(category))
        {
            return Results.Problem(
                detail: "Category is required (e.g. posters, backdrops, trailers, screen-logos, hall-logos, promotions, branch-gallery, concessions).", 
                statusCode: StatusCodes.Status400BadRequest, 
                title: "Missing Category");
        }

        await using var stream = file.OpenReadStream();
        var validation = MediaFileValidator.ValidateFile(file.FileName, file.ContentType, category, file.Length, stream);
        if (!validation.IsValid)
        {
            return Results.Problem(
                detail: validation.ErrorMessage, 
                statusCode: StatusCodes.Status400BadRequest, 
                title: "Media Validation Error");
        }

        var result = await storageService.UploadAsync(stream, file.FileName, file.ContentType, category, ct);

        return Results.Ok(new MediaUploadResponse(
            result.PublicUrl,
            result.StorageKey,
            result.ContentType,
            result.SizeBytes,
            category
        ));
    }

    public static async Task<IResult> HandlePresignUploadAsync(
        PresignUploadRequest request, 
        IStorageService storageService, 
        CancellationToken ct)
    {
        if (request == null)
        {
            return Results.Problem(
                detail: "Request body cannot be null.", 
                statusCode: StatusCodes.Status400BadRequest, 
                title: "Invalid Request");
        }

        var validation = MediaFileValidator.ValidatePresignRequest(request.FileName, request.ContentType, request.Category);
        if (!validation.IsValid)
        {
            return Results.Problem(
                detail: validation.ErrorMessage, 
                statusCode: StatusCodes.Status400BadRequest, 
                title: "Media Validation Error");
        }

        var result = await storageService.GeneratePresignedUploadUrlAsync(
            request.FileName, 
            request.ContentType, 
            request.Category, 
            TimeSpan.FromMinutes(15), 
            ct);

        return Results.Ok(new PresignedUploadResponse(
            result.UploadUrl,
            result.PublicUrl,
            result.StorageKey,
            result.ExpiresAt
        ));
    }

    public static async Task<IResult> HandleGetFileAsync(
        string category, 
        string fileName, 
        HttpContext context, 
        IStorageService storageService, 
        CancellationToken ct)
    {
        var safeCategory = MediaFileValidator.SanitizeFileName(category);
        var safeFileName = MediaFileValidator.SanitizeFileName(fileName);
        var storageKey = $"{safeCategory}/{safeFileName}";

        var fileResult = await storageService.GetFileAsync(storageKey, ct);
        if (fileResult == null)
        {
            return Results.Problem(
                detail: $"Media file '{storageKey}' not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Not Found");
        }

        // Conditional GET: check ETag / If-None-Match
        if (!string.IsNullOrEmpty(fileResult.ETag))
        {
            var ifNoneMatch = context.Request.Headers.IfNoneMatch.ToString();
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch.Trim() == fileResult.ETag.Trim())
            {
                fileResult.Dispose();
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            context.Response.Headers.ETag = fileResult.ETag;
        }

        context.Response.Headers.CacheControl = "public, max-age=86400, stale-while-revalidate=3600";
        context.Response.Headers.LastModified = fileResult.LastModified.ToString("R");
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (string.Equals(fileResult.ContentType, "image/svg+xml", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        }

        return Results.Stream(
            fileResult.ContentStream, 
            contentType: fileResult.ContentType, 
            enableRangeProcessing: true);
    }

    public static async Task<IResult> HandlePutPresignedFileAsync(
        string category, 
        string fileName, 
        HttpContext context, 
        IStorageService storageService, 
        CancellationToken ct)
    {
        var safeCategory = MediaFileValidator.SanitizeFileName(category);
        var safeFileName = MediaFileValidator.SanitizeFileName(fileName);
        var storageKey = $"{safeCategory}/{safeFileName}";

        if (storageService is LocalStorageService localStorage)
        {
            var sig = context.Request.Query["sig"].FirstOrDefault();
            var expStr = context.Request.Query["exp"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(sig) || !long.TryParse(expStr, out var expSeconds) ||
                !localStorage.ValidatePresignedSignature(storageKey, expSeconds, sig))
            {
                return Results.Problem(
                    detail: "Presigned upload URL has expired or has an invalid signature.", 
                    statusCode: StatusCodes.Status403Forbidden, 
                    title: "Forbidden");
            }

            var contentType = context.Request.ContentType ?? MediaFileValidator.GetContentTypeForExtension(Path.GetExtension(safeFileName));
            var fullPath = Path.GetFullPath(Path.Combine(localStorage.StorageRoot, safeCategory, safeFileName));
            if (!localStorage.IsWithinRoot(fullPath))
            {
                return Results.Problem(
                    detail: "Access outside root storage directory is blocked.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Storage Path");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await context.Request.Body.CopyToAsync(fileStream, ct);
            }

            var fileInfo = new FileInfo(fullPath);
            var size = fileInfo.Length;

            if (size == 0)
            {
                try { File.Delete(fullPath); } catch { }
                return Results.Problem(
                    detail: "Uploaded file cannot be empty (0 bytes).",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Empty File");
            }

            var ext = Path.GetExtension(safeFileName);
            var sizeValidation = MediaFileValidator.ValidateFileSize(size, ext);
            if (!sizeValidation.IsValid)
            {
                try { File.Delete(fullPath); } catch { }
                return Results.Problem(
                    detail: sizeValidation.ErrorMessage,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "File Size Limit Exceeded");
            }

            // Verify magic numbers on the saved file
            using (var verifyStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var magicValidation = MediaFileValidator.ValidateMagicNumbers(verifyStream, ext);
                if (!magicValidation.IsValid)
                {
                    verifyStream.Dispose();
                    try { File.Delete(fullPath); } catch { }
                    return Results.Problem(
                        detail: magicValidation.ErrorMessage,
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid File Signature");
                }
            }

            var publicUrl = $"/api/v1/media/files/{storageKey}";

            return Results.Ok(new MediaUploadResponse(
                publicUrl,
                storageKey,
                contentType,
                size,
                safeCategory
            ));
        }

        return Results.Problem(
            detail: "Direct PUT upload via this endpoint is only supported for LocalStorage provider. For S3, PUT directly to the S3 Presigned URL.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid Storage Provider Endpoint");
    }

    public static async Task<IResult> HandleDeleteAsync(
        string category, 
        string fileName, 
        IStorageService storageService, 
        CancellationToken ct)
    {
        var safeCategory = MediaFileValidator.SanitizeFileName(category);
        var safeFileName = MediaFileValidator.SanitizeFileName(fileName);
        var storageKey = $"{safeCategory}/{safeFileName}";

        var deleted = await storageService.DeleteAsync(storageKey, ct);
        if (!deleted)
        {
            return Results.Problem(
                detail: $"Media file '{storageKey}' not found.", 
                statusCode: StatusCodes.Status404NotFound, 
                title: "Not Found");
        }

        return Results.Ok(new { message = $"Media file '{storageKey}' deleted successfully." });
    }
}

namespace Catalog.Api.Models;

public record MediaUploadResponse(
    string PublicUrl,
    string StorageKey,
    string ContentType,
    long SizeBytes,
    string Category
);

public record PresignUploadRequest(
    string FileName,
    string ContentType,
    string Category
);

public record PresignedUploadResponse(
    string UploadUrl,
    string PublicUrl,
    string StorageKey,
    DateTimeOffset ExpiresAt
);

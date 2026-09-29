namespace Cinema.Foundation.Storage;

public record StorageResult(
    string PublicUrl,
    string StorageKey,
    string ContentType,
    long SizeBytes
);

public record PresignedUploadResult(
    string UploadUrl,
    string PublicUrl,
    string StorageKey,
    DateTimeOffset ExpiresAt
);

public record StorageFileResult(
    Stream ContentStream,
    string ContentType,
    string? ETag,
    DateTimeOffset LastModified,
    long SizeBytes,
    IDisposable? ParentResource = null
) : IAsyncDisposable, IDisposable
{
    public void Dispose()
    {
        ContentStream.Dispose();
        ParentResource?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await ContentStream.DisposeAsync();
        if (ParentResource is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            ParentResource?.Dispose();
        }
    }
}

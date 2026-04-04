namespace StorageServer.Storage.Models;

public sealed record UploadPartResult
{
    public required string ETag { get; init; }
}

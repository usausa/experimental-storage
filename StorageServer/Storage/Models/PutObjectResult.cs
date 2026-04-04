namespace StorageServer.Storage.Models;

public sealed record PutObjectResult
{
    public required string ETag { get; init; }
    public required string VersionId { get; init; }
}

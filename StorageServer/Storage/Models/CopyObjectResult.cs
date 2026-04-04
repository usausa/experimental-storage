namespace StorageServer.Storage.Models;

public sealed record CopyObjectResult
{
    public required string ETag { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public required string VersionId { get; init; }
}

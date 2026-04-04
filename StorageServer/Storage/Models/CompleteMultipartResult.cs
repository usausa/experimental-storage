namespace StorageServer.Storage.Models;

public sealed record CompleteMultipartResult
{
    public required string Bucket { get; init; }
    public required string Key { get; init; }
    public required string ETag { get; init; }
    public required string VersionId { get; init; }
}

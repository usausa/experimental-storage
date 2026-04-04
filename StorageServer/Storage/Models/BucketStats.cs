namespace StorageServer.Storage.Models;

public sealed record BucketStats
{
    public required string Bucket { get; init; }
    public long ObjectCount { get; init; }
    public long TotalSizeBytes { get; init; }
    public DateTimeOffset? LastModified { get; init; }
}

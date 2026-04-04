namespace StorageServer.Storage.Models;

public sealed record ObjectSummary
{
    public required string Key { get; init; }
    public long Size { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public required string ETag { get; init; }
    public string StorageClass { get; init; } = "STANDARD";
}

namespace StorageServer.Storage.Models;

public sealed record PartInfo
{
    public int PartNumber { get; init; }
    public long Size { get; init; }
    public required string ETag { get; init; }
    public DateTimeOffset LastModified { get; init; }
}

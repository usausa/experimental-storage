namespace StorageServer.Storage.Models;

public sealed record VersionInfo
{
    public required string VersionId { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public long Size { get; init; }
    public required string ETag { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsDeleteMarker { get; init; }
}

namespace StorageServer.Storage.Models;

public sealed record ObjectHead
{
    public required string Key { get; init; }
    public long ContentLength { get; init; }
    public required string ContentType { get; init; }
    public required string ETag { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public string StorageClass { get; init; } = "STANDARD";
    public string Acl { get; init; } = "private";
    public string? VersionId { get; init; }
    public required IReadOnlyDictionary<string, string> UserMetadata { get; init; }
}

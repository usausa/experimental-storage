namespace StorageServer.Storage.Models;

public sealed record ListObjectsResult
{
    public required IReadOnlyList<ObjectSummary> Objects { get; init; }
    public required IReadOnlyList<string> CommonPrefixes { get; init; }
    public bool IsTruncated { get; init; }
    public string? NextContinuationToken { get; init; }
    public int KeyCount { get; init; }
}

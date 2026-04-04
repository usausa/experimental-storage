namespace StorageServer.Storage.Models;

public sealed record GetObjectOptions
{
    public long? RangeStart { get; init; }
    public long? RangeEnd { get; init; }
    public string? IfNoneMatch { get; init; }
    public string? IfMatch { get; init; }
    public DateTimeOffset? IfModifiedSince { get; init; }
    public DateTimeOffset? IfUnmodifiedSince { get; init; }
}

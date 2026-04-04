namespace StorageServer.Storage.Models;

public sealed record ListObjectsOptions
{
    public string? Prefix { get; init; }
    public string? Delimiter { get; init; }
    public int MaxKeys { get; init; } = 1000;
    public string? StartAfter { get; init; }
    public string? ContinuationToken { get; init; }
}

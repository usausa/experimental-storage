namespace StorageServer.Storage.Models;

public sealed record DeleteObjectResult
{
    public required string Key { get; init; }
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

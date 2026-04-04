namespace StorageServer.Storage.Models;

public sealed record MultipartUploadInfo
{
    public required string UploadId { get; init; }
    public required string Bucket { get; init; }
    public required string Key { get; init; }
    public DateTimeOffset Initiated { get; init; }
}

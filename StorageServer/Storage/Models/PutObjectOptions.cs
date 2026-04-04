namespace StorageServer.Storage.Models;

public sealed record PutObjectOptions
{
    public string? ContentType { get; init; }
    public string StorageClass { get; init; } = "STANDARD";
    public string Acl { get; init; } = "private";
    public Dictionary<string, string>? UserMetadata { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
}

namespace StorageServer.Storage.Models;

public sealed record ObjectMetadataPatch
{
    public string? Acl { get; init; }
    public Dictionary<string, string>? UserMetadata { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
}

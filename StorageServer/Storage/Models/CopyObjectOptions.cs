namespace StorageServer.Storage.Models;

public sealed record CopyObjectOptions
{
    public string MetadataDirective { get; init; } = "COPY";
    public PutObjectOptions? NewMetadata { get; init; }
}

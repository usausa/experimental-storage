namespace StorageServer.Storage;

public sealed class StorageOptions
{
    public required string BasePath { get; set; }
    public int MaxVersionsPerObject { get; set; }
    public int VersionRetentionDays { get; set; }
}

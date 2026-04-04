namespace StorageServer.Storage;

public sealed class StorageOptions
{
    public required string BasePath { get; set; }
    public string BaseHostname { get; set; } = "s3.localhost";
    public int MaxVersionsPerObject { get; set; }
    public int VersionRetentionDays { get; set; }
}

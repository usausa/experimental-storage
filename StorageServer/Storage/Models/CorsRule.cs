namespace StorageServer.Storage.Models;

public sealed record CorsRule
{
    public List<string> AllowedOrigins { get; init; } = [];
    public List<string> AllowedMethods { get; init; } = [];
    public List<string> AllowedHeaders { get; init; } = [];
    public List<string> ExposeHeaders { get; init; } = [];
    public int MaxAgeSeconds { get; init; }
}

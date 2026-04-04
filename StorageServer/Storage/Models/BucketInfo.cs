namespace StorageServer.Storage.Models;

public sealed record BucketInfo(
    string Name,
    DateTimeOffset CreatedAt,
    string Region = "us-east-1");

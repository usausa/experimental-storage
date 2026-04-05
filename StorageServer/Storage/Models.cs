namespace StorageServer.Storage;

//--------------------------------------------------------------------------------
// Bucket
//--------------------------------------------------------------------------------

public sealed record BucketInfo(
    string Name,
    DateTimeOffset CreatedAt,
    string Region = "us-east-1");

public sealed record BucketStats
{
    public required string Bucket { get; init; }
    public long ObjectCount { get; init; }
    public long TotalSizeBytes { get; init; }
    public DateTimeOffset? LastModified { get; init; }
}

//--------------------------------------------------------------------------------
// Object
//--------------------------------------------------------------------------------

public sealed record ListObjectsOptions
{
    public string? Prefix { get; init; }
    public string? Delimiter { get; init; }
    public int MaxKeys { get; init; } = 1000;
    public string? StartAfter { get; init; }
    public string? ContinuationToken { get; init; }
}

public sealed record ObjectSummary
{
    public required string Key { get; init; }
    public long Size { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public required string ETag { get; init; }
    public string StorageClass { get; init; } = "STANDARD";
    public bool IsDeleted { get; init; }
}

public sealed record ListObjectsResult
{
    public required IReadOnlyList<ObjectSummary> Objects { get; init; }
    public required IReadOnlyList<string> CommonPrefixes { get; init; }
    public bool IsTruncated { get; init; }
    public string? NextContinuationToken { get; init; }
    public int KeyCount { get; init; }
}

public sealed record ObjectHead
{
    public required string Key { get; init; }
    public long ContentLength { get; init; }
    public required string ContentType { get; init; }
    public required string ETag { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public string StorageClass { get; init; } = "STANDARD";
    public string Acl { get; init; } = "private";
    public string? VersionId { get; init; }
    public required IReadOnlyDictionary<string, string> UserMetadata { get; init; }
}

public sealed record GetObjectOptions
{
    public long? RangeStart { get; init; }
    public long? RangeEnd { get; init; }
    public string? IfNoneMatch { get; init; }
    public string? IfMatch { get; init; }
    public DateTimeOffset? IfModifiedSince { get; init; }
    public DateTimeOffset? IfUnmodifiedSince { get; init; }
}

public sealed record ObjectData : IAsyncDisposable
{
    public required ObjectHead Head { get; init; }
    public required Stream Content { get; init; }

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public sealed record PutObjectOptions
{
    public string? ContentType { get; init; }
    public string StorageClass { get; init; } = "STANDARD";
    public string Acl { get; init; } = "private";
    public Dictionary<string, string>? UserMetadata { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
}

public sealed record PutObjectResult
{
    public required string ETag { get; init; }
    public required string VersionId { get; init; }
}

public sealed record DeleteObjectResult
{
    public required string Key { get; init; }
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record CopyObjectOptions
{
    public string MetadataDirective { get; init; } = "COPY";
    public PutObjectOptions? NewMetadata { get; init; }
}

public sealed record CopyObjectResult
{
    public required string ETag { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public required string VersionId { get; init; }
}

//--------------------------------------------------------------------------------
// Metadata
//--------------------------------------------------------------------------------

public sealed record ObjectMetadata
{
    public required string Key { get; init; }
    public required string ContentType { get; init; }
    public long ContentLength { get; init; }
    public required string ETag { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public string StorageClass { get; init; } = "STANDARD";
    public string Acl { get; init; } = "private";
    public string? VersionId { get; init; }
    public required IReadOnlyDictionary<string, string> UserMetadata { get; init; }
    public required IReadOnlyDictionary<string, string> Tags { get; init; }
}

public sealed record ObjectMetadataPatch
{
    public string? Acl { get; init; }
    public Dictionary<string, string>? UserMetadata { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
}

//--------------------------------------------------------------------------------
// CORS
//--------------------------------------------------------------------------------

public sealed record CorsRule
{
    public List<string> AllowedOrigins { get; init; } = [];
    public List<string> AllowedMethods { get; init; } = [];
    public List<string> AllowedHeaders { get; init; } = [];
    public List<string> ExposeHeaders { get; init; } = [];
    public int MaxAgeSeconds { get; init; }
}

//--------------------------------------------------------------------------------
// Multipart
//--------------------------------------------------------------------------------

public sealed record UploadPartResult
{
    public required string ETag { get; init; }
}

public sealed record PartInfo
{
    public int PartNumber { get; init; }
    public long Size { get; init; }
    public required string ETag { get; init; }
    public DateTimeOffset LastModified { get; init; }
}

public sealed record CompleteMultipartResult
{
    public required string Bucket { get; init; }
    public required string Key { get; init; }
    public required string ETag { get; init; }
    public required string VersionId { get; init; }
}

public sealed record MultipartUploadInfo
{
    public required string UploadId { get; init; }
    public required string Bucket { get; init; }
    public required string Key { get; init; }
    public DateTimeOffset Initiated { get; init; }
}

//--------------------------------------------------------------------------------
// Version
//--------------------------------------------------------------------------------

public sealed record VersionInfo
{
    public required string VersionId { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public long Size { get; init; }
    public required string ETag { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsDeleteMarker { get; init; }
}

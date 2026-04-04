namespace StorageServer.Storage;

using StorageServer.Storage.Models;

/// <summary>
/// Provides storage operations for buckets and objects.
/// </summary>
public interface IStorageService
{
    // Bucket operations
    Task<IReadOnlyList<BucketInfo>> ListBucketsAsync(CancellationToken ct = default);
    Task CreateBucketAsync(string bucket, CancellationToken ct = default);
    Task<bool> BucketExistsAsync(string bucket, CancellationToken ct = default);
    Task DeleteBucketAsync(string bucket, bool force = false, CancellationToken ct = default);
    Task<BucketInfo> GetBucketInfoAsync(string bucket, CancellationToken ct = default);
    Task<BucketStats> GetBucketStatsAsync(string bucket, CancellationToken ct = default);

    // Object operations
    Task<ListObjectsResult> ListObjectsAsync(string bucket, ListObjectsOptions options, CancellationToken ct = default);
    Task<ObjectHead> HeadObjectAsync(string bucket, string key, CancellationToken ct = default);
    Task<ObjectData> GetObjectAsync(string bucket, string key, GetObjectOptions? options = null, CancellationToken ct = default);
    Task<PutObjectResult> PutObjectAsync(string bucket, string key, Stream data, PutObjectOptions? options = null, CancellationToken ct = default);
    Task DeleteObjectAsync(string bucket, string key, CancellationToken ct = default);
    Task<IReadOnlyList<DeleteObjectResult>> DeleteObjectsAsync(string bucket, IEnumerable<string> keys, CancellationToken ct = default);
    Task<CopyObjectResult> CopyObjectAsync(string bucket, string key, string sourceBucket, string sourceKey, CopyObjectOptions? options = null, CancellationToken ct = default);

    // Metadata operations
    Task<ObjectMetadata> GetObjectMetadataAsync(string bucket, string key, CancellationToken ct = default);
    Task UpdateObjectMetadataAsync(string bucket, string key, ObjectMetadataPatch patch, CancellationToken ct = default);

    // Tag operations
    Task<Dictionary<string, string>> GetObjectTagsAsync(string bucket, string key, CancellationToken ct = default);
    Task PutObjectTagsAsync(string bucket, string key, Dictionary<string, string> tags, CancellationToken ct = default);
    Task DeleteObjectTagsAsync(string bucket, string key, CancellationToken ct = default);
    Task<Dictionary<string, string>> GetBucketTagsAsync(string bucket, CancellationToken ct = default);
    Task PutBucketTagsAsync(string bucket, Dictionary<string, string> tags, CancellationToken ct = default);
    Task DeleteBucketTagsAsync(string bucket, CancellationToken ct = default);

    // ACL operations
    Task<string> GetObjectAclAsync(string bucket, string key, CancellationToken ct = default);
    Task PutObjectAclAsync(string bucket, string key, string acl, CancellationToken ct = default);
    Task<string> GetBucketAclAsync(string bucket, CancellationToken ct = default);
    Task PutBucketAclAsync(string bucket, string acl, CancellationToken ct = default);

    // CORS operations
    Task<IReadOnlyList<CorsRule>> GetBucketCorsAsync(string bucket, CancellationToken ct = default);
    Task PutBucketCorsAsync(string bucket, IReadOnlyList<CorsRule> rules, CancellationToken ct = default);
    Task DeleteBucketCorsAsync(string bucket, CancellationToken ct = default);

    // Multipart upload operations
    Task<string> CreateMultipartUploadAsync(string bucket, string key, PutObjectOptions? options = null, CancellationToken ct = default);
    Task<UploadPartResult> UploadPartAsync(string uploadId, int partNumber, Stream data, CancellationToken ct = default);
    Task<CompleteMultipartResult> CompleteMultipartUploadAsync(string uploadId, IEnumerable<PartInfo> parts, CancellationToken ct = default);
    Task AbortMultipartUploadAsync(string uploadId, CancellationToken ct = default);
    Task<IReadOnlyList<MultipartUploadInfo>> ListMultipartUploadsAsync(string bucket, CancellationToken ct = default);
    Task<IReadOnlyList<PartInfo>> ListPartsAsync(string uploadId, CancellationToken ct = default);

    // Version operations
    Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(string bucket, string key, CancellationToken ct = default);
    Task<ObjectData> GetObjectVersionAsync(string bucket, string key, string versionId, CancellationToken ct = default);
    Task RestoreVersionAsync(string bucket, string key, string versionId, CancellationToken ct = default);
    Task DeleteVersionAsync(string bucket, string key, string versionId, CancellationToken ct = default);

    // Thumbnail
    Task<Stream?> GetThumbnailAsync(string bucket, string key, int maxWidth = 128, int maxHeight = 128, CancellationToken ct = default);
}

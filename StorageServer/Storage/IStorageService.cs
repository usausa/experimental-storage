namespace StorageServer.Storage;

using StorageServer.Storage.Models;

public interface IStorageService
{
    //--------------------------------------------------------------------------------
    // Bucket operations
    //--------------------------------------------------------------------------------

    ValueTask<IReadOnlyList<BucketInfo>> ListBucketsAsync(CancellationToken token = default);
    ValueTask CreateBucketAsync(string bucket, CancellationToken token = default);
    ValueTask<bool> BucketExistsAsync(string bucket, CancellationToken token = default);
    ValueTask DeleteBucketAsync(string bucket, bool force = false, CancellationToken token = default);
    ValueTask<BucketInfo> GetBucketInfoAsync(string bucket, CancellationToken token = default);
    ValueTask<BucketStats> GetBucketStatsAsync(string bucket, CancellationToken token = default);

    //--------------------------------------------------------------------------------
    // Object operations
    //--------------------------------------------------------------------------------

    ValueTask<ListObjectsResult> ListObjectsAsync(string bucket, ListObjectsOptions options, CancellationToken token = default);
    ValueTask<ObjectHead> HeadObjectAsync(string bucket, string key, CancellationToken token = default);
    ValueTask<ObjectData> GetObjectAsync(string bucket, string key, GetObjectOptions? options = null, CancellationToken token = default);
    ValueTask<PutObjectResult> PutObjectAsync(string bucket, string key, Stream data, PutObjectOptions? options = null, CancellationToken token = default);
    ValueTask DeleteObjectAsync(string bucket, string key, CancellationToken token = default);
    ValueTask<IReadOnlyList<DeleteObjectResult>> DeleteObjectsAsync(string bucket, IEnumerable<string> keys, CancellationToken token = default);
    ValueTask<CopyObjectResult> CopyObjectAsync(string bucket, string key, string sourceBucket, string sourceKey, CopyObjectOptions? options = null, CancellationToken token = default);

    //--------------------------------------------------------------------------------
    // Metadata operations
    //--------------------------------------------------------------------------------

    ValueTask<ObjectMetadata> GetObjectMetadataAsync(string bucket, string key, CancellationToken token = default);
    ValueTask UpdateObjectMetadataAsync(string bucket, string key, ObjectMetadataPatch patch, CancellationToken token = default);

    //--------------------------------------------------------------------------------
    // Tag operations
    //--------------------------------------------------------------------------------

    ValueTask<Dictionary<string, string>> GetObjectTagsAsync(string bucket, string key, CancellationToken token = default);
    ValueTask PutObjectTagsAsync(string bucket, string key, Dictionary<string, string> tags, CancellationToken token = default);
    ValueTask DeleteObjectTagsAsync(string bucket, string key, CancellationToken token = default);
    ValueTask<Dictionary<string, string>> GetBucketTagsAsync(string bucket, CancellationToken token = default);
    ValueTask PutBucketTagsAsync(string bucket, Dictionary<string, string> tags, CancellationToken token = default);
    ValueTask DeleteBucketTagsAsync(string bucket, CancellationToken token = default);

    //--------------------------------------------------------------------------------
    // ACL operations
    //--------------------------------------------------------------------------------

    ValueTask<string> GetObjectAclAsync(string bucket, string key, CancellationToken token = default);
    ValueTask PutObjectAclAsync(string bucket, string key, string acl, CancellationToken token = default);
    ValueTask<string> GetBucketAclAsync(string bucket, CancellationToken token = default);
    ValueTask PutBucketAclAsync(string bucket, string acl, CancellationToken token = default);

    //--------------------------------------------------------------------------------
    // CORS operations
    //--------------------------------------------------------------------------------

    ValueTask<IReadOnlyList<CorsRule>> GetBucketCorsAsync(string bucket, CancellationToken token = default);
    ValueTask PutBucketCorsAsync(string bucket, IReadOnlyList<CorsRule> rules, CancellationToken token = default);
    ValueTask DeleteBucketCorsAsync(string bucket, CancellationToken token = default);

    //--------------------------------------------------------------------------------
    // Multipart upload operations
    //--------------------------------------------------------------------------------

    ValueTask<string> CreateMultipartUploadAsync(string bucket, string key, PutObjectOptions? options = null, CancellationToken token = default);
    ValueTask<UploadPartResult> UploadPartAsync(string uploadId, int partNumber, Stream data, CancellationToken token = default);
    ValueTask<CompleteMultipartResult> CompleteMultipartUploadAsync(string uploadId, IEnumerable<PartInfo> parts, CancellationToken token = default);
    ValueTask AbortMultipartUploadAsync(string uploadId, CancellationToken token = default);
    ValueTask<IReadOnlyList<MultipartUploadInfo>> ListMultipartUploadsAsync(string bucket, CancellationToken token = default);
    ValueTask<IReadOnlyList<PartInfo>> ListPartsAsync(string uploadId, CancellationToken token = default);

    //--------------------------------------------------------------------------------
    // Version operations
    //--------------------------------------------------------------------------------

    ValueTask<IReadOnlyList<VersionInfo>> ListVersionsAsync(string bucket, string key, CancellationToken token = default);
    ValueTask<IReadOnlyList<ObjectSummary>> ListDeletedObjectsAsync(string bucket, string? prefix = null, CancellationToken token = default);
    ValueTask<ObjectData> GetObjectVersionAsync(string bucket, string key, string versionId, CancellationToken token = default);
    ValueTask RestoreVersionAsync(string bucket, string key, string versionId, CancellationToken token = default);
    ValueTask DeleteVersionAsync(string bucket, string key, string versionId, CancellationToken token = default);

    ValueTask PurgeObjectAsync(string bucket, string key, CancellationToken token = default);

    //--------------------------------------------------------------------------------
    // Thumbnail
    //--------------------------------------------------------------------------------

    ValueTask<Stream?> GetThumbnailAsync(string bucket, string key, int maxWidth = 128, int maxHeight = 128, CancellationToken token = default);
}

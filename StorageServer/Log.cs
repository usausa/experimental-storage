namespace StorageServer;

internal static partial class Log
{
    // Startup

    [LoggerMessage(Level = LogLevel.Information, Message = "Service start.")]
    public static partial void InfoServiceStart(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Runtime: os=[{osDescription}], framework=[{frameworkDescription}], rid=[{runtimeIdentifier}]")]
    public static partial void InfoServiceSettingsRuntime(this ILogger logger, string osDescription, string frameworkDescription, string runtimeIdentifier);

    [LoggerMessage(Level = LogLevel.Information, Message = "Environment: version=[{version}], directory=[{directory}]")]
    public static partial void InfoServiceSettingsEnvironment(this ILogger logger, Version? version, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "GCSettings: serverGC=[{isServerGC}], latencyMode=[{latencyMode}], largeObjectHeapCompactionMode=[{largeObjectHeapCompactionMode}]")]
    public static partial void InfoServiceSettingsGC(this ILogger logger, bool isServerGC, GCLatencyMode latencyMode, GCLargeObjectHeapCompactionMode largeObjectHeapCompactionMode);

    // Bucket

    [LoggerMessage(Level = LogLevel.Information, Message = "Bucket created. bucket=[{Bucket}]")]
    public static partial void InfoBucketCreated(this ILogger log, string bucket);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bucket deleted. bucket=[{Bucket}], force=[{Force}]")]
    public static partial void InfoBucketDeleted(this ILogger log, string bucket, bool force);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bucket CORS updated. bucket=[{Bucket}]")]
    public static partial void InfoBucketCorsUpdated(this ILogger log, string bucket);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bucket CORS deleted. bucket=[{Bucket}]")]
    public static partial void InfoBucketCorsDeleted(this ILogger log, string bucket);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bucket ACL updated. bucket=[{Bucket}], acl=[{Acl}]")]
    public static partial void InfoBucketAclUpdated(this ILogger log, string bucket, string acl);

    // Object

    [LoggerMessage(Level = LogLevel.Debug, Message = "Object put. bucket=[{Bucket}], key=[{Key}], versionId=[{VersionId}]")]
    public static partial void DebugObjectPut(this ILogger log, string bucket, string key, string versionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Object deleted. bucket=[{Bucket}], key=[{Key}]")]
    public static partial void DebugObjectDeleted(this ILogger log, string bucket, string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "Object copied. sourceBucket=[{SourceBucket}], sourceKey=[{SourceKey}], bucket=[{Bucket}], key=[{Key}], versionId=[{VersionId}]")]
    public static partial void InfoObjectCopied(this ILogger log, string sourceBucket, string sourceKey, string bucket, string key, string versionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Object purged. bucket=[{Bucket}], key=[{Key}]")]
    public static partial void InfoObjectPurged(this ILogger log, string bucket, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Object delete failed. bucket=[{Bucket}], key=[{Key}], error=[{Error}]")]
    public static partial void WarnObjectDeleteFailed(this ILogger log, string bucket, string key, string error);

    // Version

    [LoggerMessage(Level = LogLevel.Information, Message = "Version restored. bucket=[{Bucket}], key=[{Key}], versionId=[{VersionId}]")]
    public static partial void InfoVersionRestored(this ILogger log, string versionId, string bucket, string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "Version deleted. bucket=[{Bucket}], key=[{Key}], versionId=[{VersionId}]")]
    public static partial void InfoVersionDeleted(this ILogger log, string versionId, string bucket, string key);

    // Multipart

    [LoggerMessage(Level = LogLevel.Debug, Message = "Multipart upload created. uploadId=[{UploadId}], bucket=[{Bucket}], key=[{Key}]")]
    public static partial void DebugMultipartUploadCreated(this ILogger log, string uploadId, string bucket, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Multipart upload completed. uploadId=[{UploadId}], bucket=[{Bucket}], key=[{Key}]")]
    public static partial void DebugMultipartUploadCompleted(this ILogger log, string uploadId, string bucket, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Multipart upload aborted. uploadId=[{UploadId}]")]
    public static partial void DebugMultipartUploadAborted(this ILogger log, string uploadId);

    // S3 Middleware

    [LoggerMessage(Level = LogLevel.Warning, Message = "S3 storage error. requestId=[{RequestId}], code=[{ErrorCode}], status=[{StatusCode}], message=[{Message}]")]
    public static partial void WarnS3StorageError(this ILogger log, string requestId, string errorCode, int statusCode, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "S3 storage error. requestId=[{RequestId}], code=[{ErrorCode}], status=[{StatusCode}], message=[{Message}]")]
    public static partial void ErrorS3StorageError(this ILogger log, string requestId, string errorCode, int statusCode, string message);
}

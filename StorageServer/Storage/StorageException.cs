namespace StorageServer.Storage;

public class StorageException(string errorCode, int httpStatusCode, string message)
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public int HttpStatusCode { get; } = httpStatusCode;
}

public class BucketNotFoundException(string bucket)
    : StorageException("NoSuchBucket", 404, $"The specified bucket '{bucket}' does not exist.");

public class ObjectNotFoundException(string bucket, string key)
    : StorageException("NoSuchKey", 404, $"The specified key '{key}' does not exist in bucket '{bucket}'.");

public class BucketNotEmptyException(string bucket)
    : StorageException("BucketNotEmpty", 409, $"The bucket '{bucket}' is not empty.");

public class BucketAlreadyExistsException(string bucket)
    : StorageException("BucketAlreadyOwnedByYou", 409, $"The bucket '{bucket}' already exists.");

public class InvalidBucketNameException(string bucket, string reason)
    : StorageException("InvalidBucketName", 400, $"The bucket name '{bucket}' is invalid: {reason}");

public class InvalidObjectKeyException(string key, string reason)
    : StorageException("InvalidArgument", 400, $"The object key '{key}' is invalid: {reason}");

public class MultipartUploadNotFoundException(string uploadId)
    : StorageException("NoSuchUpload", 404, $"The specified multipart upload '{uploadId}' does not exist.");

public class VersionNotFoundException(string bucket, string key, string versionId)
    : StorageException("NoSuchVersion", 404, $"The specified version '{versionId}' of '{key}' in bucket '{bucket}' does not exist.");

public class PreconditionFailedException(string message)
    : StorageException("PreconditionFailed", 412, message);

public class NotModifiedException()
    : StorageException("NotModified", 304, "Not modified.");

public class CorsConfigNotFoundException(string bucket)
    : StorageException("NoSuchCORSConfiguration", 404, $"The CORS configuration for bucket '{bucket}' does not exist.");

namespace StorageServer.Consts;

using System.Xml.Linq;

// ReSharper disable InconsistentNaming
public static class S3Names
{
    public static readonly XNamespace S3Ns = "http://s3.amazonaws.com/doc/2006-03-01/";
    public static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

    public static readonly XName XmlnsXsi = XNamespace.Xmlns + "xsi";

    public static readonly XName Error = S3Ns + "Error";
    public static readonly XName Code = S3Ns + "Code";
    public static readonly XName Message = S3Ns + "Message";
    public static readonly XName RequestId = S3Ns + "RequestId";
    public static readonly XName ListAllMyBucketsResult = S3Ns + "ListAllMyBucketsResult";
    public static readonly XName Owner = S3Ns + "Owner";
    public static readonly XName ID = S3Ns + "ID";
    public static readonly XName DisplayName = S3Ns + "DisplayName";
    public static readonly XName Buckets = S3Ns + "Buckets";
    public static readonly XName Bucket = S3Ns + "Bucket";
    public static readonly XName Name = S3Ns + "Name";
    public static readonly XName CreationDate = S3Ns + "CreationDate";
    public static readonly XName Prefix = S3Ns + "Prefix";
    public static readonly XName KeyCount = S3Ns + "KeyCount";
    public static readonly XName MaxKeys = S3Ns + "MaxKeys";
    public static readonly XName IsTruncated = S3Ns + "IsTruncated";
    public static readonly XName Delimiter = S3Ns + "Delimiter";
    public static readonly XName NextContinuationToken = S3Ns + "NextContinuationToken";
    public static readonly XName StartAfter = S3Ns + "StartAfter";
    public static readonly XName Contents = S3Ns + "Contents";
    public static readonly XName Key = S3Ns + "Key";
    public static readonly XName LastModified = S3Ns + "LastModified";
    public static readonly XName ETag = S3Ns + "ETag";
    public static readonly XName Size = S3Ns + "Size";
    public static readonly XName StorageClass = S3Ns + "StorageClass";
    public static readonly XName CommonPrefixes = S3Ns + "CommonPrefixes";
    public static readonly XName ListBucketResult = S3Ns + "ListBucketResult";
    public static readonly XName LocationConstraint = S3Ns + "LocationConstraint";
    public static readonly XName CopyObjectResult = S3Ns + "CopyObjectResult";
    public static readonly XName Tagging = S3Ns + "Tagging";
    public static readonly XName TagSet = S3Ns + "TagSet";
    public static readonly XName Tag = S3Ns + "Tag";
    public static readonly XName Value = S3Ns + "Value";
    public static readonly XName AccessControlPolicy = S3Ns + "AccessControlPolicy";
    public static readonly XName AccessControlList = S3Ns + "AccessControlList";
    public static readonly XName Grant = S3Ns + "Grant";
    public static readonly XName Grantee = S3Ns + "Grantee";
    public static readonly XName URI = S3Ns + "URI";
    public static readonly XName Permission = S3Ns + "Permission";
    public static readonly XName CORSConfiguration = S3Ns + "CORSConfiguration";
    public static readonly XName CORSRule = S3Ns + "CORSRule";
    public static readonly XName AllowedOrigin = S3Ns + "AllowedOrigin";
    public static readonly XName AllowedMethod = S3Ns + "AllowedMethod";
    public static readonly XName AllowedHeader = S3Ns + "AllowedHeader";
    public static readonly XName ExposeHeader = S3Ns + "ExposeHeader";
    public static readonly XName MaxAgeSeconds = S3Ns + "MaxAgeSeconds";
    public static readonly XName Deleted = S3Ns + "Deleted";
    public static readonly XName DeleteResult = S3Ns + "DeleteResult";
    public static readonly XName InitiateMultipartUploadResult = S3Ns + "InitiateMultipartUploadResult";
    public static readonly XName UploadId = S3Ns + "UploadId";
    public static readonly XName CompleteMultipartUploadResult = S3Ns + "CompleteMultipartUploadResult";
    public static readonly XName ListMultipartUploadsResult = S3Ns + "ListMultipartUploadsResult";
    public static readonly XName Upload = S3Ns + "Upload";
    public static readonly XName Initiated = S3Ns + "Initiated";
    public static readonly XName ListPartsResult = S3Ns + "ListPartsResult";
    public static readonly XName Part = S3Ns + "Part";
    public static readonly XName PartNumber = S3Ns + "PartNumber";
    public static readonly XName VersioningConfiguration = S3Ns + "VersioningConfiguration";
    public static readonly XName Status = S3Ns + "Status";
    public static readonly XName XsiType = XsiNs + "type";
}
// ReSharper restore InconsistentNaming

namespace StorageServer.Api.S3;

using System.Xml.Linq;

using StorageServer.Storage.Models;

/// <summary>
/// Helper methods for building and parsing S3-compatible XML documents.
/// </summary>
public static class S3XmlHelper
{
    private static readonly XNamespace S3Ns = "http://s3.amazonaws.com/doc/2006-03-01/";

    public static IResult Xml(XDocument doc)
    {
        return Results.Content(doc.Declaration + doc.ToString(), "application/xml");
    }

    public static XDocument ListAllMyBucketsResult(IReadOnlyList<BucketInfo> buckets)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "ListAllMyBucketsResult",
                new XElement(S3Ns + "Owner",
                    new XElement(S3Ns + "ID", "owner"),
                    new XElement(S3Ns + "DisplayName", "owner")),
                new XElement(S3Ns + "Buckets",
                    buckets.Select(b => new XElement(S3Ns + "Bucket",
                        new XElement(S3Ns + "Name", b.Name),
                        new XElement(S3Ns + "CreationDate", b.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")))))));
    }

    public static XDocument ListBucketResult(string bucket, ListObjectsResult result, ListObjectsOptions options)
    {
        var elements = new List<object>
        {
            new XElement(S3Ns + "Name", bucket),
            new XElement(S3Ns + "Prefix", options.Prefix ?? string.Empty),
            new XElement(S3Ns + "KeyCount", result.KeyCount),
            new XElement(S3Ns + "MaxKeys", options.MaxKeys),
            new XElement(S3Ns + "IsTruncated", result.IsTruncated.ToString().ToLowerInvariant())
        };

        if (options.Delimiter != null)
        {
            elements.Add(new XElement(S3Ns + "Delimiter", options.Delimiter));
        }

        if (result.NextContinuationToken != null)
        {
            elements.Add(new XElement(S3Ns + "NextContinuationToken", result.NextContinuationToken));
        }

        if (options.StartAfter != null)
        {
            elements.Add(new XElement(S3Ns + "StartAfter", options.StartAfter));
        }

        foreach (var obj in result.Objects)
        {
            elements.Add(new XElement(S3Ns + "Contents",
                new XElement(S3Ns + "Key", obj.Key),
                new XElement(S3Ns + "LastModified", obj.LastModified.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")),
                new XElement(S3Ns + "ETag", obj.ETag),
                new XElement(S3Ns + "Size", obj.Size),
                new XElement(S3Ns + "StorageClass", obj.StorageClass)));
        }

        foreach (var prefix in result.CommonPrefixes)
        {
            elements.Add(new XElement(S3Ns + "CommonPrefixes",
                new XElement(S3Ns + "Prefix", prefix)));
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "ListBucketResult", elements));
    }

    public static XDocument LocationConstraint(string region)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "LocationConstraint", region));
    }

    public static XDocument CopyObjectResult(CopyObjectResult result)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "CopyObjectResult",
                new XElement(S3Ns + "LastModified", result.LastModified.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")),
                new XElement(S3Ns + "ETag", result.ETag)));
    }

    public static XDocument Tagging(Dictionary<string, string> tags)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "Tagging",
                new XElement(S3Ns + "TagSet",
                    tags.Select(t => new XElement(S3Ns + "Tag",
                        new XElement(S3Ns + "Key", t.Key),
                        new XElement(S3Ns + "Value", t.Value))))));
    }

    public static Dictionary<string, string> ParseTagging(XDocument doc)
    {
        var tags = new Dictionary<string, string>();
        var tagSet = doc.Descendants().Where(e => e.Name.LocalName == "Tag");
        foreach (var tag in tagSet)
        {
            var key = tag.Elements().FirstOrDefault(e => e.Name.LocalName == "Key")?.Value;
            var value = tag.Elements().FirstOrDefault(e => e.Name.LocalName == "Value")?.Value;
            if (key != null)
            {
                tags[key] = value ?? string.Empty;
            }
        }
        return tags;
    }

    public static XDocument AccessControlPolicy(string acl)
    {
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

        var grants = new List<XElement>
        {
            new(S3Ns + "Grant",
                new XElement(S3Ns + "Grantee",
                    new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName),
                    new XAttribute(xsi + "type", "CanonicalUser"),
                    new XElement(S3Ns + "ID", "owner"),
                    new XElement(S3Ns + "DisplayName", "owner")),
                new XElement(S3Ns + "Permission", "FULL_CONTROL"))
        };

        if (string.Equals(acl, "public-read", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(acl, "public-read-write", StringComparison.OrdinalIgnoreCase))
        {
            grants.Add(new XElement(S3Ns + "Grant",
                new XElement(S3Ns + "Grantee",
                    new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName),
                    new XAttribute(xsi + "type", "Group"),
                    new XElement(S3Ns + "URI", "http://acs.amazonaws.com/groups/global/AllUsers")),
                new XElement(S3Ns + "Permission", "READ")));
        }

        if (string.Equals(acl, "public-read-write", StringComparison.OrdinalIgnoreCase))
        {
            grants.Add(new XElement(S3Ns + "Grant",
                new XElement(S3Ns + "Grantee",
                    new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName),
                    new XAttribute(xsi + "type", "Group"),
                    new XElement(S3Ns + "URI", "http://acs.amazonaws.com/groups/global/AllUsers")),
                new XElement(S3Ns + "Permission", "WRITE")));
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "AccessControlPolicy",
                new XElement(S3Ns + "Owner",
                    new XElement(S3Ns + "ID", "owner"),
                    new XElement(S3Ns + "DisplayName", "owner")),
                new XElement(S3Ns + "AccessControlList", grants)));
    }

    public static XDocument CorsConfiguration(IReadOnlyList<CorsRule> rules)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "CORSConfiguration",
                rules.Select(r => new XElement(S3Ns + "CORSRule",
                    r.AllowedOrigins.Select(o => new XElement(S3Ns + "AllowedOrigin", o)),
                    r.AllowedMethods.Select(m => new XElement(S3Ns + "AllowedMethod", m)),
                    r.AllowedHeaders.Select(h => new XElement(S3Ns + "AllowedHeader", h)),
                    r.ExposeHeaders.Select(h => new XElement(S3Ns + "ExposeHeader", h)),
                    r.MaxAgeSeconds > 0 ? new XElement(S3Ns + "MaxAgeSeconds", r.MaxAgeSeconds) : null))));
    }

    public static List<CorsRule> ParseCorsConfiguration(XDocument doc)
    {
        return doc.Descendants()
            .Where(e => e.Name.LocalName == "CORSRule")
            .Select(r => new CorsRule
            {
                AllowedOrigins = r.Elements().Where(e => e.Name.LocalName == "AllowedOrigin").Select(e => e.Value).ToList(),
                AllowedMethods = r.Elements().Where(e => e.Name.LocalName == "AllowedMethod").Select(e => e.Value).ToList(),
                AllowedHeaders = r.Elements().Where(e => e.Name.LocalName == "AllowedHeader").Select(e => e.Value).ToList(),
                ExposeHeaders = r.Elements().Where(e => e.Name.LocalName == "ExposeHeader").Select(e => e.Value).ToList(),
                MaxAgeSeconds = int.TryParse(r.Elements().FirstOrDefault(e => e.Name.LocalName == "MaxAgeSeconds")?.Value, out var s) ? s : 0
            }).ToList();
    }

    public static XDocument DeleteResult(IReadOnlyList<DeleteObjectResult> results)
    {
        var elements = new List<XElement>();
        foreach (var r in results)
        {
            if (r.Success)
            {
                elements.Add(new XElement(S3Ns + "Deleted", new XElement(S3Ns + "Key", r.Key)));
            }
            else
            {
                elements.Add(new XElement(
                    S3Ns + "Error",
                    new XElement(S3Ns + "Key", r.Key),
                    new XElement(S3Ns + "Code", r.ErrorCode ?? "InternalError"),
                    new XElement(S3Ns + "Message", r.ErrorMessage ?? "Unknown error")));
            }
        }
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "DeleteResult", elements));
    }

    public static List<string> ParseDeleteObjects(XDocument doc)
    {
        return doc.Descendants()
            .Where(e => e.Name.LocalName == "Object")
            .Select(e => e.Elements().FirstOrDefault(k => k.Name.LocalName == "Key")?.Value)
            .Where(k => k != null)
            .Cast<string>()
            .ToList();
    }

    public static XDocument InitiateMultipartUploadResult(string bucket, string key, string uploadId)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "InitiateMultipartUploadResult",
                new XElement(S3Ns + "Bucket", bucket),
                new XElement(S3Ns + "Key", key),
                new XElement(S3Ns + "UploadId", uploadId)));
    }

    public static XDocument CompleteMultipartUploadResult(CompleteMultipartResult result)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "CompleteMultipartUploadResult",
                new XElement(S3Ns + "Bucket", result.Bucket),
                new XElement(S3Ns + "Key", result.Key),
                new XElement(S3Ns + "ETag", result.ETag)));
    }

    public static List<PartInfo> ParseCompleteMultipartUpload(XDocument doc)
    {
        return doc.Descendants()
            .Where(e => e.Name.LocalName == "Part")
            .Select(p => new PartInfo
            {
                PartNumber = int.TryParse(p.Elements().FirstOrDefault(e => e.Name.LocalName == "PartNumber")?.Value, out var n) ? n : 0,
                ETag = p.Elements().FirstOrDefault(e => e.Name.LocalName == "ETag")?.Value ?? string.Empty
            }).ToList();
    }

    public static XDocument ListMultipartUploadsResult(string bucket, IReadOnlyList<MultipartUploadInfo> uploads)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "ListMultipartUploadsResult",
                new XElement(S3Ns + "Bucket", bucket),
                uploads.Select(u => new XElement(S3Ns + "Upload",
                    new XElement(S3Ns + "Key", u.Key),
                    new XElement(S3Ns + "UploadId", u.UploadId),
                    new XElement(S3Ns + "Initiated", u.Initiated.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))))));
    }

    public static XDocument ListPartsResult(string bucket, string key, string uploadId, IReadOnlyList<PartInfo> parts)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "ListPartsResult",
                new XElement(S3Ns + "Bucket", bucket),
                new XElement(S3Ns + "Key", key),
                new XElement(S3Ns + "UploadId", uploadId),
                parts.Select(p => new XElement(S3Ns + "Part",
                    new XElement(S3Ns + "PartNumber", p.PartNumber),
                    new XElement(S3Ns + "ETag", p.ETag),
                    new XElement(S3Ns + "Size", p.Size),
                    new XElement(S3Ns + "LastModified", p.LastModified.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))))));
    }

    /// <summary>
    /// Builds a VersioningConfiguration XML response.
    /// </summary>
    public static XDocument VersioningConfiguration(string status)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "VersioningConfiguration",
                new XElement(S3Ns + "Status", status)));
    }
}

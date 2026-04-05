namespace StorageServer.Endpoints.S3;

using System.Globalization;
using System.Xml.Linq;

using Microsoft.AspNetCore.Mvc;

using StorageServer.Consts;
using StorageServer.Helpers;
using StorageServer.Storage;
using StorageServer.Storage.Models;

/// <summary>
/// Maps S3-compatible Minimal API endpoints under /storage/*.
/// </summary>
public static class S3Endpoints
{
    public static void MapS3Endpoints(this WebApplication app)
    {
        app.MapGet("/storage/", HandleListBuckets);

        app.MapGet("/storage/{bucket}", HandleBucketGet);
        app.MapPut("/storage/{bucket}", HandleBucketPut);
        app.MapMethods("/storage/{bucket}", ["HEAD"], HandleBucketHead);
        app.MapDelete("/storage/{bucket}", HandleBucketDelete);
        app.MapPost("/storage/{bucket}", HandleBucketPost);

        app.MapGet("/storage/{bucket}/{**key}", HandleObjectGet);
        app.MapPut("/storage/{bucket}/{**key}", HandleObjectPut);
        app.MapMethods("/storage/{bucket}/{**key}", ["HEAD"], HandleObjectHead);
        app.MapDelete("/storage/{bucket}/{**key}", HandleObjectDelete);
        app.MapPost("/storage/{bucket}/{**key}", HandleObjectPost);
    }

    // ===== ListBuckets =====

    private static async Task<IResult> HandleListBuckets(IStorageService storage)
    {
        var buckets = await storage.ListBucketsAsync();
        return Xml(new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Names.ListAllMyBucketsResult,
                new XElement(S3Names.Owner,
                    new XElement(S3Names.ID, "owner"),
                    new XElement(S3Names.DisplayName, "owner")),
                new XElement(S3Names.Buckets,
                    buckets.Select(b => new XElement(S3Names.Bucket,
                        new XElement(S3Names.Name, b.Name),
                        new XElement(S3Names.CreationDate, b.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture))))))));
    }

    // ===== Query parameter records =====

    private sealed record BucketGetQuery(
        string? Location,
        string? Tagging,
        string? Acl,
        string? Cors,
        string? Versioning,
        string? Lifecycle,
        string? Policy,
        string? Logging,
        string? Notification,
        string? Encryption,
        string? Uploads,
        string? Prefix,
        string? Delimiter,
        [property: FromQuery(Name = "max-keys")] int? MaxKeys,
        [property: FromQuery(Name = "start-after")] string? StartAfter,
        [property: FromQuery(Name = "continuation-token")] string? ContinuationToken);

    private sealed record BucketPutQuery(
        string? Tagging,
        string? Acl,
        string? Cors,
        string? Versioning,
        string? Lifecycle,
        string? Policy,
        string? Logging,
        string? Notification,
        string? Encryption);

    private sealed record BucketDeleteQuery(
        string? Tagging,
        string? Cors);

    private sealed record BucketPostQuery(
        string? Delete);

    private sealed record ObjectGetQuery(
        string? Tagging,
        string? Acl,
        string? UploadId);

    private sealed record ObjectPutQuery(
        string? Tagging,
        string? Acl,
        int? PartNumber,
        string? UploadId);

    private sealed record ObjectDeleteQuery(
        string? Tagging,
        string? UploadId);

    private sealed record ObjectPostQuery(
        string? Uploads,
        string? UploadId);

    // ===== Bucket GET =====

    private static async Task<IResult> HandleBucketGet(
        string bucket,
        [AsParameters] BucketGetQuery query,
        IStorageService storage)
    {
        if (query.Location is not null)
        {
            var info = await storage.GetBucketInfoAsync(bucket);
            return Xml(new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(S3Names.LocationConstraint, info.Region)));
        }
        if (query.Tagging is not null)
        {
            var tags = await storage.GetBucketTagsAsync(bucket);
            return Xml(BuildTagging(tags));
        }
        if (query.Acl is not null)
        {
            var acl = await storage.GetBucketAclAsync(bucket);
            return Xml(BuildAccessControlPolicy(acl));
        }
        if (query.Cors is not null)
        {
            var cors = await storage.GetBucketCorsAsync(bucket);
            return Xml(BuildCorsConfiguration(cors));
        }
        if (query.Versioning is not null)
        {
            return Xml(new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(S3Names.VersioningConfiguration,
                    new XElement(S3Names.Status, "Enabled"))));
        }
        if (query.Lifecycle is not null || query.Policy is not null ||
            query.Logging is not null || query.Notification is not null ||
            query.Encryption is not null)
        {
            return Results.NoContent();
        }
        if (query.Uploads is not null)
        {
            var uploads = await storage.ListMultipartUploadsAsync(bucket);
            return Xml(new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(S3Names.ListMultipartUploadsResult,
                    new XElement(S3Names.Bucket, bucket),
                    uploads.Select(u => new XElement(S3Names.Upload,
                        new XElement(S3Names.Key, u.Key),
                        new XElement(S3Names.UploadId, u.UploadId),
                        new XElement(S3Names.Initiated, u.Initiated.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)))))));
        }

        var options = new ListObjectsOptions
        {
            Prefix = query.Prefix,
            Delimiter = query.Delimiter,
            MaxKeys = query.MaxKeys ?? 1000,
            StartAfter = query.StartAfter,
            ContinuationToken = query.ContinuationToken
        };
        var result = await storage.ListObjectsAsync(bucket, options);
        return Xml(BuildListBucketResult(bucket, result, options));
    }

    // ===== Bucket PUT =====

    private static async Task<IResult> HandleBucketPut(
        HttpContext context,
        string bucket,
        [AsParameters] BucketPutQuery query,
        IStorageService storage)
    {
        if (query.Tagging is not null)
        {
            var doc = await XDocument.LoadAsync(context.Request.Body, LoadOptions.None, context.RequestAborted);
            var tags = ParseTagging(doc);
            await storage.PutBucketTagsAsync(bucket, tags);
            return Results.Ok();
        }
        if (query.Acl is not null)
        {
            var aclValue = context.Request.Headers["x-amz-acl"].FirstOrDefault() ?? "private";
            await storage.PutBucketAclAsync(bucket, aclValue);
            return Results.Ok();
        }
        if (query.Cors is not null)
        {
            var doc = await XDocument.LoadAsync(context.Request.Body, LoadOptions.None, context.RequestAborted);
            var rules = ParseCorsConfiguration(doc);
            await storage.PutBucketCorsAsync(bucket, rules);
            return Results.Ok();
        }
        if (query.Versioning is not null || query.Lifecycle is not null || query.Policy is not null ||
            query.Logging is not null || query.Notification is not null || query.Encryption is not null)
        {
            return Results.Ok();
        }

        await storage.CreateBucketAsync(bucket);
        return Results.Ok();
    }

    // ===== Bucket HEAD =====

    private static async Task<IResult> HandleBucketHead(
        string bucket,
        IStorageService storage)
    {
        var exists = await storage.BucketExistsAsync(bucket);
        return exists ? Results.Ok() : Results.NotFound();
    }

    // ===== Bucket DELETE =====

    private static async Task<IResult> HandleBucketDelete(
        string bucket,
        [AsParameters] BucketDeleteQuery query,
        IStorageService storage)
    {
        if (query.Tagging is not null)
        {
            await storage.DeleteBucketTagsAsync(bucket);
            return Results.NoContent();
        }
        if (query.Cors is not null)
        {
            await storage.DeleteBucketCorsAsync(bucket);
            return Results.NoContent();
        }

        await storage.DeleteBucketAsync(bucket);
        return Results.NoContent();
    }

    // ===== Bucket POST (DeleteObjects) =====

    private static async Task<IResult> HandleBucketPost(
        HttpContext context,
        string bucket,
        [AsParameters] BucketPostQuery query,
        IStorageService storage)
    {
        if (query.Delete is not null)
        {
            var doc = await XDocument.LoadAsync(context.Request.Body, LoadOptions.None, context.RequestAborted);
            var keys = ParseDeleteObjects(doc);
            var results = await storage.DeleteObjectsAsync(bucket, keys);
            return Xml(BuildDeleteResult(results));
        }
        return Results.BadRequest();
    }

    // ===== Object GET =====

    private static async Task<IResult> HandleObjectGet(
        HttpContext context,
        string bucket,
        string key,
        [AsParameters] ObjectGetQuery query,
        IStorageService storage)
    {
        if (query.Tagging is not null)
        {
            var tags = await storage.GetObjectTagsAsync(bucket, key);
            return Xml(BuildTagging(tags));
        }
        if (query.Acl is not null)
        {
            var aclValue = await storage.GetObjectAclAsync(bucket, key);
            return Xml(BuildAccessControlPolicy(aclValue));
        }
        if (query.UploadId is not null)
        {
            var parts = await storage.ListPartsAsync(query.UploadId);
            return Xml(new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(S3Names.ListPartsResult,
                    new XElement(S3Names.Bucket, bucket),
                    new XElement(S3Names.Key, key),
                    new XElement(S3Names.UploadId, query.UploadId),
                    parts.Select(p => new XElement(S3Names.Part,
                        new XElement(S3Names.PartNumber, p.PartNumber),
                        new XElement(S3Names.ETag, p.ETag),
                        new XElement(S3Names.Size, p.Size),
                        new XElement(S3Names.LastModified, p.LastModified.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)))))));
        }

        var options = new GetObjectOptions();
        var headers = context.Request.Headers;

        if (headers.TryGetValue("Range", out var rangeValues))
        {
            var rangeHeader = rangeValues.First()!;
            if (rangeHeader.StartsWith("bytes=", StringComparison.Ordinal))
            {
                var range = rangeHeader["bytes=".Length..];
                var rangeParts = range.Split('-');
                var start = Int64.TryParse(rangeParts[0], out var s) ? s : (long?)null;
                var end = rangeParts.Length > 1 && Int64.TryParse(rangeParts[1], out var e) ? e : (long?)null;
                options = options with { RangeStart = start, RangeEnd = end };
            }
        }

        if (headers.TryGetValue("If-None-Match", out var ifNoneMatchValues))
        {
            options = options with { IfNoneMatch = ifNoneMatchValues.First() };
        }

        if (headers.TryGetValue("If-Match", out var ifMatchValues))
        {
            options = options with { IfMatch = ifMatchValues.First() };
        }

        if (headers.TryGetValue("If-Modified-Since", out var ifModifiedSinceValues) && DateTimeOffset.TryParse(ifModifiedSinceValues.First(), out var ims))
        {
            options = options with { IfModifiedSince = ims };
        }

        if (headers.TryGetValue("If-Unmodified-Since", out var ifUnmodifiedSinceValues) && DateTimeOffset.TryParse(ifUnmodifiedSinceValues.First(), out var ius))
        {
            options = options with { IfUnmodifiedSince = ius };
        }

        await using var data = await storage.GetObjectAsync(bucket, key, options);

        context.Response.ContentType = data.Head.ContentType;
        context.Response.ContentLength = data.Head.ContentLength;
        context.Response.Headers["ETag"] = data.Head.ETag;
        context.Response.Headers["Last-Modified"] = data.Head.LastModified.ToString("R");
        if (data.Head.VersionId is not null)
        {
            context.Response.Headers["x-amz-version-id"] = data.Head.VersionId;
        }

        foreach (var (k, v) in data.Head.UserMetadata)
        {
            context.Response.Headers[$"x-amz-meta-{k}"] = v;
        }

        await data.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        return Results.Empty;
    }

    // ===== Object PUT =====

    private static async Task<IResult> HandleObjectPut(
        HttpContext context,
        string bucket,
        string key,
        [AsParameters] ObjectPutQuery query,
        IStorageService storage)
    {
        var headers = context.Request.Headers;

        if (query.Tagging is not null)
        {
            var doc = await XDocument.LoadAsync(context.Request.Body, LoadOptions.None, context.RequestAborted);
            var tags = ParseTagging(doc);
            await storage.PutObjectTagsAsync(bucket, key, tags);
            return Results.Ok();
        }
        if (query.Acl is not null)
        {
            var aclValue = headers["x-amz-acl"].FirstOrDefault() ?? "private";
            await storage.PutObjectAclAsync(bucket, key, aclValue);
            return Results.Ok();
        }

        // UploadPart
        if (query.PartNumber is not null && query.UploadId is not null)
        {
            var partBody = GetBodyStream(context);
            var partResult = await storage.UploadPartAsync(query.UploadId, query.PartNumber.Value, partBody);
            context.Response.Headers["ETag"] = partResult.ETag;
            return Results.Ok();
        }

        // CopyObject
        var copySource = headers["x-amz-copy-source"].FirstOrDefault();
        if (copySource is not null)
        {
            copySource = Uri.UnescapeDataString(copySource);
            if (copySource.StartsWith('/'))
            {
                copySource = copySource[1..];
            }
            var slashIndex = copySource.IndexOf('/', StringComparison.Ordinal);
            var srcBucket = copySource[..slashIndex];
            var srcKey = copySource[(slashIndex + 1)..];

            var directive = headers["x-amz-metadata-directive"].FirstOrDefault() ?? "COPY";
            var copyOptions = new CopyObjectOptions { MetadataDirective = directive };

            if (String.Equals(directive, "REPLACE", StringComparison.OrdinalIgnoreCase))
            {
                copyOptions = copyOptions with
                {
                    NewMetadata = ExtractPutOptions(headers)
                };
            }

            var copyResult = await storage.CopyObjectAsync(bucket, key, srcBucket, srcKey, copyOptions);
            return Xml(new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(S3Names.CopyObjectResult,
                    new XElement(S3Names.LastModified, copyResult.LastModified.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)),
                    new XElement(S3Names.ETag, copyResult.ETag))));
        }

        // PutObject
        var putOptions = ExtractPutOptions(headers);
        var bodyStream = GetBodyStream(context);
        var putResult = await storage.PutObjectAsync(bucket, key, bodyStream, putOptions);
        context.Response.Headers["ETag"] = putResult.ETag;
        context.Response.Headers["x-amz-version-id"] = putResult.VersionId;
        return Results.Ok();
    }

    // ===== Object HEAD =====

    private static async Task<IResult> HandleObjectHead(
        HttpContext context,
        string bucket,
        string key,
        IStorageService storage)
    {
        var head = await storage.HeadObjectAsync(bucket, key);

        context.Response.ContentType = head.ContentType;
        context.Response.ContentLength = head.ContentLength;
        context.Response.Headers["ETag"] = head.ETag;
        context.Response.Headers["Last-Modified"] = head.LastModified.ToString("R");
        context.Response.Headers["x-amz-storage-class"] = head.StorageClass;
        if (head.VersionId is not null)
        {
            context.Response.Headers["x-amz-version-id"] = head.VersionId;
        }

        foreach (var (k, v) in head.UserMetadata)
        {
            context.Response.Headers[$"x-amz-meta-{k}"] = v;
        }

        return Results.Empty;
    }

    // ===== Object DELETE =====

    private static async Task<IResult> HandleObjectDelete(
        string bucket,
        string key,
        [AsParameters] ObjectDeleteQuery query,
        IStorageService storage)
    {
        if (query.Tagging is not null)
        {
            await storage.DeleteObjectTagsAsync(bucket, key);
            return Results.NoContent();
        }
        if (query.UploadId is not null)
        {
            await storage.AbortMultipartUploadAsync(query.UploadId);
            return Results.NoContent();
        }

        await storage.DeleteObjectAsync(bucket, key);
        return Results.NoContent();
    }

    // ===== Object POST =====

    private static async Task<IResult> HandleObjectPost(
        HttpContext context,
        string bucket,
        string key,
        [AsParameters] ObjectPostQuery query,
        IStorageService storage)
    {
        if (query.Uploads is not null)
        {
            var options = ExtractPutOptions(context.Request.Headers);
            var newUploadId = await storage.CreateMultipartUploadAsync(bucket, key, options);
            return Xml(new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(S3Names.InitiateMultipartUploadResult,
                    new XElement(S3Names.Bucket, bucket),
                    new XElement(S3Names.Key, key),
                    new XElement(S3Names.UploadId, newUploadId))));
        }

        if (query.UploadId is not null)
        {
            var doc = await XDocument.LoadAsync(context.Request.Body, LoadOptions.None, context.RequestAborted);
            var parts = ParseCompleteMultipartUpload(doc);
            var result = await storage.CompleteMultipartUploadAsync(query.UploadId, parts);
            return Xml(new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(S3Names.CompleteMultipartUploadResult,
                    new XElement(S3Names.Bucket, result.Bucket),
                    new XElement(S3Names.Key, result.Key),
                    new XElement(S3Names.ETag, result.ETag))));
        }

        return Results.BadRequest();
    }

    // ===== XML helpers =====

    private static IResult Xml(XDocument doc) =>
        Results.Content(doc.Declaration + doc.ToString(), "application/xml");

    private static XDocument BuildTagging(Dictionary<string, string> tags) =>
        new(new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Names.Tagging,
                new XElement(S3Names.TagSet,
                    tags.Select(t => new XElement(S3Names.Tag,
                        new XElement(S3Names.Key, t.Key),
                        new XElement(S3Names.Value, t.Value))))));

    private static XDocument BuildAccessControlPolicy(string acl)
    {
        var grants = new List<XElement>
        {
            new(S3Names.Grant,
                new XElement(S3Names.Grantee,
                    new XAttribute(S3Names.XmlnsXsi, S3Names.XsiNs.NamespaceName),
                    new XAttribute(S3Names.XsiType, "CanonicalUser"),
                    new XElement(S3Names.ID, "owner"),
                    new XElement(S3Names.DisplayName, "owner")),
                new XElement(S3Names.Permission, "FULL_CONTROL"))
        };

        if (String.Equals(acl, "public-read", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(acl, "public-read-write", StringComparison.OrdinalIgnoreCase))
        {
            grants.Add(new XElement(S3Names.Grant,
                new XElement(S3Names.Grantee,
                    new XAttribute(S3Names.XmlnsXsi, S3Names.XsiNs.NamespaceName),
                    new XAttribute(S3Names.XsiType, "Group"),
                    new XElement(S3Names.URI, "http://acs.amazonaws.com/groups/global/AllUsers")),
                new XElement(S3Names.Permission, "READ")));
        }

        if (String.Equals(acl, "public-read-write", StringComparison.OrdinalIgnoreCase))
        {
            grants.Add(new XElement(S3Names.Grant,
                new XElement(S3Names.Grantee,
                    new XAttribute(S3Names.XmlnsXsi, S3Names.XsiNs.NamespaceName),
                    new XAttribute(S3Names.XsiType, "Group"),
                    new XElement(S3Names.URI, "http://acs.amazonaws.com/groups/global/AllUsers")),
                new XElement(S3Names.Permission, "WRITE")));
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Names.AccessControlPolicy,
                new XElement(S3Names.Owner,
                    new XElement(S3Names.ID, "owner"),
                    new XElement(S3Names.DisplayName, "owner")),
                new XElement(S3Names.AccessControlList, grants)));
    }

    private static XDocument BuildCorsConfiguration(IEnumerable<CorsRule> rules) =>
        new(new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Names.CORSConfiguration,
                rules.Select(r => new XElement(S3Names.CORSRule,
                    r.AllowedOrigins.Select(o => new XElement(S3Names.AllowedOrigin, o)),
                    r.AllowedMethods.Select(m => new XElement(S3Names.AllowedMethod, m)),
                    r.AllowedHeaders.Select(h => new XElement(S3Names.AllowedHeader, h)),
                    r.ExposeHeaders.Select(h => new XElement(S3Names.ExposeHeader, h)),
                    r.MaxAgeSeconds > 0 ? new XElement(S3Names.MaxAgeSeconds, r.MaxAgeSeconds) : null))));

    private static XDocument BuildListBucketResult(string bucket, ListObjectsResult result, ListObjectsOptions options)
    {
#pragma warning disable CA1308
        var elements = new List<object>
        {
            new XElement(S3Names.Name, bucket),
            new XElement(S3Names.Prefix, options.Prefix ?? string.Empty),
            new XElement(S3Names.KeyCount, result.KeyCount),
            new XElement(S3Names.MaxKeys, options.MaxKeys),
            new XElement(S3Names.IsTruncated, result.IsTruncated.ToString().ToLowerInvariant())
        };
#pragma warning restore CA1308

        if (options.Delimiter is not null)
        {
            elements.Add(new XElement(S3Names.Delimiter, options.Delimiter));
        }

        if (result.NextContinuationToken is not null)
        {
            elements.Add(new XElement(S3Names.NextContinuationToken, result.NextContinuationToken));
        }

        if (options.StartAfter is not null)
        {
            elements.Add(new XElement(S3Names.StartAfter, options.StartAfter));
        }

        foreach (var obj in result.Objects)
        {
            elements.Add(new XElement(S3Names.Contents,
                new XElement(S3Names.Key, obj.Key),
                new XElement(S3Names.LastModified, obj.LastModified.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)),
                new XElement(S3Names.ETag, obj.ETag),
                new XElement(S3Names.Size, obj.Size),
                new XElement(S3Names.StorageClass, obj.StorageClass)));
        }

        foreach (var prefix in result.CommonPrefixes)
        {
            elements.Add(new XElement(S3Names.CommonPrefixes,
                new XElement(S3Names.Prefix, prefix)));
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Names.ListBucketResult, elements));
    }

    private static XDocument BuildDeleteResult(IEnumerable<DeleteObjectResult> results)
    {
        var elements = new List<XElement>();
        foreach (var r in results)
        {
            if (r.Success)
            {
                elements.Add(new XElement(S3Names.Deleted, new XElement(S3Names.Key, r.Key)));
            }
            else
            {
                elements.Add(new XElement(S3Names.Error,
                    new XElement(S3Names.Key, r.Key),
                    new XElement(S3Names.Code, r.ErrorCode ?? "InternalError"),
                    new XElement(S3Names.Message, r.ErrorMessage ?? "Unknown error")));
            }
        }
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Names.DeleteResult, elements));
    }

    // ===== Parse helpers =====

    private static Dictionary<string, string> ParseTagging(XDocument doc)
    {
        var tags = new Dictionary<string, string>();
        foreach (var tag in doc.Descendants().Where(e => e.Name.LocalName == "Tag"))
        {
            var key = tag.Elements().FirstOrDefault(e => e.Name.LocalName == "Key")?.Value;
            var value = tag.Elements().FirstOrDefault(e => e.Name.LocalName == "Value")?.Value;
            if (key is not null)
            {
                tags[key] = value ?? string.Empty;
            }
        }
        return tags;
    }

    private static List<CorsRule> ParseCorsConfiguration(XDocument doc) =>
        doc.Descendants()
            .Where(e => e.Name.LocalName == "CORSRule")
            .Select(r => new CorsRule
            {
                AllowedOrigins = r.Elements().Where(e => e.Name.LocalName == "AllowedOrigin").Select(e => e.Value).ToList(),
                AllowedMethods = r.Elements().Where(e => e.Name.LocalName == "AllowedMethod").Select(e => e.Value).ToList(),
                AllowedHeaders = r.Elements().Where(e => e.Name.LocalName == "AllowedHeader").Select(e => e.Value).ToList(),
                ExposeHeaders = r.Elements().Where(e => e.Name.LocalName == "ExposeHeader").Select(e => e.Value).ToList(),
                MaxAgeSeconds = Int32.TryParse(r.Elements().FirstOrDefault(e => e.Name.LocalName == "MaxAgeSeconds")?.Value, out var s) ? s : 0
            }).ToList();

    private static List<string> ParseDeleteObjects(XDocument doc) =>
        doc.Descendants()
            .Where(e => e.Name.LocalName == "Object")
            .Select(e => e.Elements().FirstOrDefault(k => k.Name.LocalName == "Key")?.Value)
            .Where(k => k is not null)
            .Cast<string>()
            .ToList();

    private static List<PartInfo> ParseCompleteMultipartUpload(XDocument doc) =>
        doc.Descendants()
            .Where(e => e.Name.LocalName == "Part")
            .Select(p => new PartInfo
            {
                PartNumber = Int32.TryParse(p.Elements().FirstOrDefault(e => e.Name.LocalName == "PartNumber")?.Value, out var n) ? n : 0,
                ETag = p.Elements().FirstOrDefault(e => e.Name.LocalName == "ETag")?.Value ?? string.Empty
            }).ToList();

    // ===== Request helpers =====

    private static Stream GetBodyStream(HttpContext context)
    {
        var headers = context.Request.Headers;
        var contentEncoding = headers["Content-Encoding"].FirstOrDefault() ?? string.Empty;
        var contentSha256 = headers["x-amz-content-sha256"].FirstOrDefault() ?? string.Empty;

        if (contentEncoding.Contains("aws-chunked", StringComparison.OrdinalIgnoreCase) ||
            contentSha256.StartsWith("STREAMING-", StringComparison.OrdinalIgnoreCase))
        {
            return new ChunkedStream(context.Request.Body);
        }

        return context.Request.Body;
    }

    private static PutObjectOptions ExtractPutOptions(IHeaderDictionary headers)
    {
        var options = new PutObjectOptions();
        var userMeta = new Dictionary<string, string>();
        var contentType = headers.ContentType.FirstOrDefault();

        if (contentType is not null)
        {
            options = options with { ContentType = contentType };
        }

        var acl = headers["x-amz-acl"].FirstOrDefault();
        if (acl is not null)
        {
            options = options with { Acl = acl };
        }

        var storageClass = headers["x-amz-storage-class"].FirstOrDefault();
        if (storageClass is not null)
        {
            options = options with { StorageClass = storageClass };
        }

        foreach (var header in headers)
        {
            if (header.Key.StartsWith("x-amz-meta-", StringComparison.OrdinalIgnoreCase))
            {
                var metaKey = header.Key["x-amz-meta-".Length..];
                userMeta[metaKey] = header.Value.FirstOrDefault() ?? string.Empty;
            }
        }

        if (userMeta.Count > 0)
        {
            options = options with { UserMetadata = userMeta };
        }

        return options;
    }
}

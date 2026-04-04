namespace StorageServer.Api.S3;

using System.Xml.Linq;

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
        return S3Helper.Xml(S3Helper.ListAllMyBucketsResult(buckets));
    }

    // ===== Bucket GET =====

    private static async Task<IResult> HandleBucketGet(
        string bucket, HttpContext ctx, IStorageService storage)
    {
        var query = ctx.Request.Query;

        if (query.ContainsKey("location"))
        {
            var info = await storage.GetBucketInfoAsync(bucket);
            return S3Helper.Xml(S3Helper.LocationConstraint(info.Region));
        }
        if (query.ContainsKey("tagging"))
        {
            var tags = await storage.GetBucketTagsAsync(bucket);
            return S3Helper.Xml(S3Helper.Tagging(tags));
        }
        if (query.ContainsKey("acl"))
        {
            var acl = await storage.GetBucketAclAsync(bucket);
            return S3Helper.Xml(S3Helper.AccessControlPolicy(acl));
        }
        if (query.ContainsKey("cors"))
        {
            var cors = await storage.GetBucketCorsAsync(bucket);
            return S3Helper.Xml(S3Helper.CorsConfiguration(cors));
        }
        if (query.ContainsKey("versioning"))
        {
            return S3Helper.Xml(S3Helper.VersioningConfiguration("Enabled"));
        }
        if (query.ContainsKey("lifecycle") || query.ContainsKey("policy") ||
            query.ContainsKey("logging") || query.ContainsKey("notification") ||
            query.ContainsKey("encryption"))
        {
            return Results.NoContent();
        }
        if (query.ContainsKey("uploads"))
        {
            var uploads = await storage.ListMultipartUploadsAsync(bucket);
            return S3Helper.Xml(S3Helper.ListMultipartUploadsResult(bucket, uploads));
        }

        var options = new ListObjectsOptions
        {
            Prefix = query["prefix"].FirstOrDefault(),
            Delimiter = query["delimiter"].FirstOrDefault(),
            MaxKeys = int.TryParse(query["max-keys"].FirstOrDefault(), out var mk) ? mk : 1000,
            StartAfter = query["start-after"].FirstOrDefault(),
            ContinuationToken = query["continuation-token"].FirstOrDefault()
        };
        var result = await storage.ListObjectsAsync(bucket, options);
        return S3Helper.Xml(S3Helper.ListBucketResult(bucket, result, options));
    }

    // ===== Bucket PUT =====

    private static async Task<IResult> HandleBucketPut(
        string bucket, HttpContext ctx, IStorageService storage)
    {
        var query = ctx.Request.Query;

        if (query.ContainsKey("tagging"))
        {
            var doc = await XDocument.LoadAsync(ctx.Request.Body, LoadOptions.None, ctx.RequestAborted);
            var tags = S3Helper.ParseTagging(doc);
            await storage.PutBucketTagsAsync(bucket, tags);
            return Results.Ok();
        }
        if (query.ContainsKey("acl"))
        {
            var acl = ctx.Request.Headers["x-amz-acl"].FirstOrDefault() ?? "private";
            await storage.PutBucketAclAsync(bucket, acl);
            return Results.Ok();
        }
        if (query.ContainsKey("cors"))
        {
            var doc = await XDocument.LoadAsync(ctx.Request.Body, LoadOptions.None, ctx.RequestAborted);
            var rules = S3Helper.ParseCorsConfiguration(doc);
            await storage.PutBucketCorsAsync(bucket, rules);
            return Results.Ok();
        }
        if (query.ContainsKey("versioning") || query.ContainsKey("lifecycle") ||
            query.ContainsKey("policy") || query.ContainsKey("logging") ||
            query.ContainsKey("notification") || query.ContainsKey("encryption"))
        {
            return Results.Ok();
        }

        await storage.CreateBucketAsync(bucket);
        return Results.Ok();
    }

    // ===== Bucket HEAD =====

    private static async Task<IResult> HandleBucketHead(string bucket, IStorageService storage)
    {
        var exists = await storage.BucketExistsAsync(bucket);
        return exists ? Results.Ok() : Results.NotFound();
    }

    // ===== Bucket DELETE =====

    private static async Task<IResult> HandleBucketDelete(
        string bucket, HttpContext ctx, IStorageService storage)
    {
        var query = ctx.Request.Query;

        if (query.ContainsKey("tagging"))
        {
            await storage.DeleteBucketTagsAsync(bucket);
            return Results.NoContent();
        }
        if (query.ContainsKey("cors"))
        {
            await storage.DeleteBucketCorsAsync(bucket);
            return Results.NoContent();
        }

        await storage.DeleteBucketAsync(bucket);
        return Results.NoContent();
    }

    // ===== Bucket POST (DeleteObjects) =====

    private static async Task<IResult> HandleBucketPost(
        string bucket, HttpContext ctx, IStorageService storage)
    {
        if (ctx.Request.Query.ContainsKey("delete"))
        {
            var doc = await XDocument.LoadAsync(ctx.Request.Body, LoadOptions.None, ctx.RequestAborted);
            var keys = S3Helper.ParseDeleteObjects(doc);
            var results = await storage.DeleteObjectsAsync(bucket, keys);
            return S3Helper.Xml(S3Helper.DeleteResult(results));
        }
        return Results.BadRequest();
    }

    // ===== Object GET =====

    private static async Task<IResult> HandleObjectGet(
        string bucket, string key, HttpContext ctx, IStorageService storage)
    {
        var query = ctx.Request.Query;

        if (query.ContainsKey("tagging"))
        {
            var tags = await storage.GetObjectTagsAsync(bucket, key);
            return S3Helper.Xml(S3Helper.Tagging(tags));
        }
        if (query.ContainsKey("acl"))
        {
            var acl = await storage.GetObjectAclAsync(bucket, key);
            return S3Helper.Xml(S3Helper.AccessControlPolicy(acl));
        }
        if (query.ContainsKey("uploadId"))
        {
            var uploadId = query["uploadId"].First()!;
            var parts = await storage.ListPartsAsync(uploadId);
            return S3Helper.Xml(S3Helper.ListPartsResult(bucket, key, uploadId, parts));
        }

        var options = new GetObjectOptions();
        var headers = ctx.Request.Headers;

        if (headers.ContainsKey("Range"))
        {
            var rangeHeader = headers["Range"].First()!;
            if (rangeHeader.StartsWith("bytes=", StringComparison.Ordinal))
            {
                var range = rangeHeader["bytes=".Length..];
                var rangeParts = range.Split('-');
                var start = long.TryParse(rangeParts[0], out var s) ? s : (long?)null;
                var end = rangeParts.Length > 1 && long.TryParse(rangeParts[1], out var e) ? e : (long?)null;
                options = options with { RangeStart = start, RangeEnd = end };
            }
        }

        if (headers.ContainsKey("If-None-Match"))
        {
            options = options with { IfNoneMatch = headers["If-None-Match"].First() };
        }

        if (headers.ContainsKey("If-Match"))
        {
            options = options with { IfMatch = headers["If-Match"].First() };
        }

        if (headers.ContainsKey("If-Modified-Since") && DateTimeOffset.TryParse(headers.IfModifiedSince.First(), out var ims))
        {
            options = options with { IfModifiedSince = ims };
        }

        if (headers.ContainsKey("If-Unmodified-Since") && DateTimeOffset.TryParse(headers["If-Unmodified-Since"].First(), out var ius))
        {
            options = options with { IfUnmodifiedSince = ius };
        }

        await using var data = await storage.GetObjectAsync(bucket, key, options);

        ctx.Response.ContentType = data.Head.ContentType;
        ctx.Response.ContentLength = data.Head.ContentLength;
        ctx.Response.Headers["ETag"] = data.Head.ETag;
        ctx.Response.Headers["Last-Modified"] = data.Head.LastModified.ToString("R");
        if (data.Head.VersionId != null)
        {
            ctx.Response.Headers["x-amz-version-id"] = data.Head.VersionId;
        }

        foreach (var (k, v) in data.Head.UserMetadata)
        {
            ctx.Response.Headers[$"x-amz-meta-{k}"] = v;
        }

        await data.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
        return Results.Empty;
    }

    // ===== Object PUT =====

    private static async Task<IResult> HandleObjectPut(
        string bucket, string key, HttpContext ctx, IStorageService storage)
    {
        var query = ctx.Request.Query;
        var headers = ctx.Request.Headers;

        if (query.ContainsKey("tagging"))
        {
            var doc = await XDocument.LoadAsync(ctx.Request.Body, LoadOptions.None, ctx.RequestAborted);
            var tags = S3Helper.ParseTagging(doc);
            await storage.PutObjectTagsAsync(bucket, key, tags);
            return Results.Ok();
        }
        if (query.ContainsKey("acl"))
        {
            var acl = headers["x-amz-acl"].FirstOrDefault() ?? "private";
            await storage.PutObjectAclAsync(bucket, key, acl);
            return Results.Ok();
        }

        // UploadPart
        if (query.ContainsKey("partNumber") && query.ContainsKey("uploadId"))
        {
            var uploadId = query["uploadId"].First()!;
            var partNumber = int.Parse(query["partNumber"].First()!);
            var partBody = GetBodyStream(ctx);
            var partResult = await storage.UploadPartAsync(uploadId, partNumber, partBody);
            ctx.Response.Headers["ETag"] = partResult.ETag;
            return Results.Ok();
        }

        // CopyObject
        var copySource = headers["x-amz-copy-source"].FirstOrDefault();
        if (copySource != null)
        {
            copySource = Uri.UnescapeDataString(copySource);
            if (copySource.StartsWith('/'))
            {
                copySource = copySource[1..];
            }
            var slashIdx = copySource.IndexOf('/');
            var srcBucket = copySource[..slashIdx];
            var srcKey = copySource[(slashIdx + 1)..];

            var directive = headers["x-amz-metadata-directive"].FirstOrDefault() ?? "COPY";
            var copyOptions = new CopyObjectOptions { MetadataDirective = directive };

            if (string.Equals(directive, "REPLACE", StringComparison.OrdinalIgnoreCase))
            {
                copyOptions = copyOptions with
                {
                    NewMetadata = ExtractPutOptions(headers)
                };
            }

            var copyResult = await storage.CopyObjectAsync(bucket, key, srcBucket, srcKey, copyOptions);
            return S3Helper.Xml(S3Helper.CopyObjectResult(copyResult));
        }

        // PutObject
        var putOptions = ExtractPutOptions(headers);
        var bodyStream = GetBodyStream(ctx);
        var putResult = await storage.PutObjectAsync(bucket, key, bodyStream, putOptions);
        ctx.Response.Headers["ETag"] = putResult.ETag;
        if (putResult.VersionId != null)
        {
            ctx.Response.Headers["x-amz-version-id"] = putResult.VersionId;
        }
        return Results.Ok();
    }

    // ===== Object HEAD =====

    private static async Task<IResult> HandleObjectHead(
        string bucket, string key, HttpContext ctx, IStorageService storage)
    {
        var head = await storage.HeadObjectAsync(bucket, key);

        ctx.Response.ContentType = head.ContentType;
        ctx.Response.ContentLength = head.ContentLength;
        ctx.Response.Headers["ETag"] = head.ETag;
        ctx.Response.Headers["Last-Modified"] = head.LastModified.ToString("R");
        ctx.Response.Headers["x-amz-storage-class"] = head.StorageClass;
        if (head.VersionId != null)
        {
            ctx.Response.Headers["x-amz-version-id"] = head.VersionId;
        }

        foreach (var (k, v) in head.UserMetadata)
        {
            ctx.Response.Headers[$"x-amz-meta-{k}"] = v;
        }

        return Results.Empty;
    }

    // ===== Object DELETE =====

    private static async Task<IResult> HandleObjectDelete(
        string bucket, string key, HttpContext ctx, IStorageService storage)
    {
        var query = ctx.Request.Query;

        if (query.ContainsKey("tagging"))
        {
            await storage.DeleteObjectTagsAsync(bucket, key);
            return Results.NoContent();
        }
        if (query.ContainsKey("uploadId"))
        {
            var uploadId = query["uploadId"].First()!;
            await storage.AbortMultipartUploadAsync(uploadId);
            return Results.NoContent();
        }

        await storage.DeleteObjectAsync(bucket, key);
        return Results.NoContent();
    }

    // ===== Object POST =====

    private static async Task<IResult> HandleObjectPost(
        string bucket, string key, HttpContext ctx, IStorageService storage)
    {
        var query = ctx.Request.Query;

        if (query.ContainsKey("uploads"))
        {
            var options = ExtractPutOptions(ctx.Request.Headers);
            var uploadId = await storage.CreateMultipartUploadAsync(bucket, key, options);
            return S3Helper.Xml(S3Helper.InitiateMultipartUploadResult(bucket, key, uploadId));
        }

        if (query.ContainsKey("uploadId"))
        {
            var uploadId = query["uploadId"].First()!;
            var doc = await XDocument.LoadAsync(ctx.Request.Body, LoadOptions.None, ctx.RequestAborted);
            var parts = S3Helper.ParseCompleteMultipartUpload(doc);
            var result = await storage.CompleteMultipartUploadAsync(uploadId, parts);
            return S3Helper.Xml(S3Helper.CompleteMultipartUploadResult(result));
        }

        return Results.BadRequest();
    }

    // ===== Helpers =====

    private static Stream GetBodyStream(HttpContext ctx)
    {
        var headers = ctx.Request.Headers;
        var contentEncoding = headers["Content-Encoding"].FirstOrDefault() ?? string.Empty;
        var contentSha256 = headers["x-amz-content-sha256"].FirstOrDefault() ?? string.Empty;

        if (contentEncoding.Contains("aws-chunked", StringComparison.OrdinalIgnoreCase) ||
            contentSha256.StartsWith("STREAMING-", StringComparison.OrdinalIgnoreCase))
        {
            return new ChunkedStream(ctx.Request.Body);
        }

        return ctx.Request.Body;
    }

    private static PutObjectOptions ExtractPutOptions(IHeaderDictionary headers)
    {
        var options = new PutObjectOptions();
        var userMeta = new Dictionary<string, string>();
        var contentType = headers.ContentType.FirstOrDefault();

        if (contentType != null)
        {
            options = options with { ContentType = contentType };
        }

        var acl = headers["x-amz-acl"].FirstOrDefault();
        if (acl != null)
        {
            options = options with { Acl = acl };
        }

        var storageClass = headers["x-amz-storage-class"].FirstOrDefault();
        if (storageClass != null)
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

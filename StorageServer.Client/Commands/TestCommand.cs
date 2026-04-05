namespace StorageServer.Client.Commands;

using System.Net;
using System.Text;

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

using Smart.CommandLine.Hosting;

[Command("test", "Run all S3 storage operation tests")]
public sealed class TestCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public TestCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", DefaultValue = "test-bucket")]
    public string BucketName { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        // ── 1. Create bucket ────────────────────────────────────────────
        Console.WriteLine("=== Create Bucket ===");
        await client.PutBucketAsync(BucketName);
        Console.WriteLine($"  Created: {BucketName}");

        // ── 2. Upload objects with hierarchical keys and metadata ────────
        Console.WriteLine("\n=== Upload Objects ===");
        string[] keys =
        [
            "readme.txt",
            "docs/guide.txt",
            "docs/api/reference.txt",
            "images/logo.png",
            "images/icons/favicon.ico"
        ];
        foreach (var key in keys)
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = BucketName,
                Key = key,
                ContentBody = $"Content of {key}",
                ContentType = "text/plain",
                Metadata = { ["author"] = "demo-user" }
            });
            Console.WriteLine($"  Uploaded: {key}");
        }

        // ── 3. List all objects (flat) ──────────────────────────────────
        Console.WriteLine("\n=== List All Objects (flat) ===");
        var flatList = await client.ListObjectsV2Async(
            new ListObjectsV2Request { BucketName = BucketName });
        foreach (var obj in flatList.S3Objects)
        {
            Console.WriteLine($"  {obj.Key} ({obj.Size} bytes)");
        }

        // ── 4. List with delimiter (hierarchy browsing) ─────────────────
        Console.WriteLine("\n=== Root Level (delimiter='/') ===");
        var rootList = await client.ListObjectsV2Async(
            new ListObjectsV2Request { BucketName = BucketName, Delimiter = "/" });
        foreach (var cp in rootList.CommonPrefixes)
        {
            Console.WriteLine($"  [DIR]  {cp}");
        }
        foreach (var obj in rootList.S3Objects)
        {
            Console.WriteLine($"  [FILE] {obj.Key}");
        }

        // ── 5. Pagination (MaxKeys=2) ───────────────────────────────────
        Console.WriteLine("\n=== Pagination (MaxKeys=2) ===");
        string? continuationToken = null;
        var page = 1;
        do
        {
            var resp = await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = BucketName,
                MaxKeys = 2,
                ContinuationToken = continuationToken
            });
            Console.WriteLine($"  Page {page}: " +
                $"{string.Join(", ", resp.S3Objects.Select(o => o.Key))} " +
                $"(IsTruncated={resp.IsTruncated})");
            continuationToken = resp.IsTruncated == true ? resp.NextContinuationToken : null;
            page++;
        }
        while (continuationToken is not null);

        // ── 6. CopyObject ───────────────────────────────────────────────
        Console.WriteLine("\n=== Copy Object ===");
        await client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = BucketName,
            SourceKey = "readme.txt",
            DestinationBucket = BucketName,
            DestinationKey = "backup/readme-copy.txt"
        });
        Console.WriteLine("  Copied readme.txt -> backup/readme-copy.txt");
        var copyGet = await client.GetObjectAsync(BucketName, "backup/readme-copy.txt");
        using (var reader = new StreamReader(copyGet.ResponseStream))
        {
            Console.WriteLine($"  Content: {await reader.ReadToEndAsync()}");
        }

        // ── 7. Content-Type and user-defined metadata ───────────────────
        Console.WriteLine("\n=== Content-Type & User Metadata ===");
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = BucketName,
            Key = "data/config.json",
            ContentBody = """{"setting": true}""",
            ContentType = "application/json",
            Metadata =
            {
                ["project"] = "storage-server",
                ["version"] = "1.0"
            }
        });
        Console.WriteLine("  Uploaded data/config.json with custom metadata");

        var metaHead = await client.GetObjectMetadataAsync(BucketName, "data/config.json");
        Console.WriteLine($"  ContentType : {metaHead.Headers.ContentType}");
        Console.WriteLine($"  x-amz-meta-project: {metaHead.Metadata["project"]}");
        Console.WriteLine($"  x-amz-meta-version: {metaHead.Metadata["version"]}");

        // Copy with COPY directive (metadata preserved)
        await client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = BucketName,
            SourceKey = "data/config.json",
            DestinationBucket = BucketName,
            DestinationKey = "data/config-copy.json"
        });
        var copyMeta = await client.GetObjectMetadataAsync(BucketName, "data/config-copy.json");
        Console.WriteLine($"  COPY directive  -> ContentType: {copyMeta.Headers.ContentType}" +
            $", project={copyMeta.Metadata["project"]}");

        // Copy with REPLACE directive (new metadata)
        await client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = BucketName,
            SourceKey = "data/config.json",
            DestinationBucket = BucketName,
            DestinationKey = "data/config-replaced.json",
            MetadataDirective = S3MetadataDirective.REPLACE,
            ContentType = "text/yaml",
            Metadata = { ["project"] = "new-project" }
        });
        var replacedMeta = await client.GetObjectMetadataAsync(BucketName, "data/config-replaced.json");
        Console.WriteLine($"  REPLACE directive -> ContentType: {replacedMeta.Headers.ContentType}" +
            $", project={replacedMeta.Metadata["project"]}");

        // ── 8. Range request (partial download) ─────────────────────────
        Console.WriteLine("\n=== Range Request ===");
        var rangeResp = await client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = BucketName,
            Key = "readme.txt",
            ByteRange = new ByteRange(0, 6)
        });
        using (var reader = new StreamReader(rangeResp.ResponseStream))
        {
            Console.WriteLine($"  Bytes 0-6: \"{await reader.ReadToEndAsync()}\"");
        }

        // ── 9. Conditional request (If-None-Match → 304) ────────────────
        Console.WriteLine("\n=== Conditional Request ===");
        var headResp = await client.GetObjectMetadataAsync(BucketName, "readme.txt");
        Console.WriteLine($"  Current ETag: {headResp.ETag}");
        try
        {
            await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = BucketName,
                Key = "readme.txt",
                EtagToNotMatch = headResp.ETag
            });
            ConsoleHelper.WriteError("  FAIL: expected 304 Not Modified");
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotModified)
        {
            Console.WriteLine("  304 Not Modified (as expected)");
        }

        // ── 10. Multipart upload ────────────────────────────────────────
        Console.WriteLine("\n=== Multipart Upload ===");
        var initResp = await client.InitiateMultipartUploadAsync(
            new InitiateMultipartUploadRequest
            {
                BucketName = BucketName,
                Key = "large-file.dat"
            });
        var uploadId = initResp.UploadId;
        Console.WriteLine($"  UploadId: {uploadId}");

        var partETags = new List<PartETag>();
        for (var i = 1; i <= 3; i++)
        {
            var partData = Encoding.UTF8.GetBytes($"[Part-{i} data with some padding...]");
            using var ms = new MemoryStream(partData);
            var partResp = await client.UploadPartAsync(new UploadPartRequest
            {
                BucketName = BucketName,
                Key = "large-file.dat",
                UploadId = uploadId,
                PartNumber = i,
                InputStream = ms
            });
            partETags.Add(new PartETag(i, partResp.ETag));
            Console.WriteLine($"  Part {i} uploaded, ETag: {partResp.ETag}");
        }

        await client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
        {
            BucketName = BucketName,
            Key = "large-file.dat",
            UploadId = uploadId,
            PartETags = partETags
        });
        Console.WriteLine("  Multipart upload completed");

        var mpGet = await client.GetObjectAsync(BucketName, "large-file.dat");
        using (var reader = new StreamReader(mpGet.ResponseStream))
        {
            Console.WriteLine($"  Content: {await reader.ReadToEndAsync()}");
        }

        // ── 11. Object tagging ──────────────────────────────────────────
        Console.WriteLine("\n=== Object Tagging ===");
        await client.PutObjectTaggingAsync(new PutObjectTaggingRequest
        {
            BucketName = BucketName,
            Key = "readme.txt",
            Tagging = new Tagging
            {
                TagSet =
                [
                    new Tag { Key = "environment", Value = "dev" },
                    new Tag { Key = "team", Value = "backend" }
                ]
            }
        });
        Console.WriteLine("  Tags set on readme.txt");

        var objTags = await client.GetObjectTaggingAsync(
            new GetObjectTaggingRequest { BucketName = BucketName, Key = "readme.txt" });
        foreach (var tag in objTags.Tagging)
        {
            Console.WriteLine($"  {tag.Key} = {tag.Value}");
        }

        await client.DeleteObjectTaggingAsync(
            new DeleteObjectTaggingRequest { BucketName = BucketName, Key = "readme.txt" });
        Console.WriteLine("  Tags deleted from readme.txt");

        // ── 12. Bucket tagging ──────────────────────────────────────────
        Console.WriteLine("\n=== Bucket Tagging ===");
        await client.PutBucketTaggingAsync(new PutBucketTaggingRequest
        {
            BucketName = BucketName,
            TagSet =
            [
                new Tag { Key = "project", Value = "storage-server" },
                new Tag { Key = "cost-center", Value = "engineering" }
            ]
        });
        Console.WriteLine("  Tags set on bucket");

        var bucketTags = await client.GetBucketTaggingAsync(
            new GetBucketTaggingRequest { BucketName = BucketName });
        foreach (var tag in bucketTags.TagSet)
        {
            Console.WriteLine($"  {tag.Key} = {tag.Value}");
        }

        await client.DeleteBucketTaggingAsync(BucketName);
        Console.WriteLine("  Tags deleted from bucket");

        // ── 13. ListMultipartUploads / ListParts ────────────────────────
        Console.WriteLine("\n=== ListMultipartUploads / ListParts ===");
        var mpInit = await client.InitiateMultipartUploadAsync(
            new InitiateMultipartUploadRequest
            {
                BucketName = BucketName,
                Key = "pending-upload.dat"
            });

        var partBytes = "test-part-data"u8.ToArray();
        using (var ms = new MemoryStream(partBytes))
        {
            await client.UploadPartAsync(new UploadPartRequest
            {
                BucketName = BucketName,
                Key = "pending-upload.dat",
                UploadId = mpInit.UploadId,
                PartNumber = 1,
                InputStream = ms
            });
        }

        var uploads = await client.ListMultipartUploadsAsync(
            new ListMultipartUploadsRequest { BucketName = BucketName });
        foreach (var u in uploads.MultipartUploads)
        {
            Console.WriteLine($"  Upload: key={u.Key}, uploadId={u.UploadId}");
        }

        var partsResp = await client.ListPartsAsync(new ListPartsRequest
        {
            BucketName = BucketName,
            Key = "pending-upload.dat",
            UploadId = mpInit.UploadId
        });
        foreach (var p in partsResp.Parts)
        {
            Console.WriteLine($"  Part {p.PartNumber}: {p.Size} bytes, ETag={p.ETag}");
        }

        await client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
        {
            BucketName = BucketName,
            Key = "pending-upload.dat",
            UploadId = mpInit.UploadId
        });
        Console.WriteLine("  Multipart upload aborted");

        // ── 14. Storage Class ───────────────────────────────────────────
        Console.WriteLine("\n=== Storage Class ===");
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = BucketName,
            Key = "archive/old-data.txt",
            ContentBody = "archived content",
            StorageClass = S3StorageClass.StandardInfrequentAccess
        });
        Console.WriteLine("  Uploaded archive/old-data.txt with STANDARD_IA");
        var scHead = await client.GetObjectMetadataAsync(BucketName, "archive/old-data.txt");
        Console.WriteLine($"  StorageClass from HEAD: {scHead.StorageClass}");

        var scList = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = BucketName,
            Prefix = "archive/"
        });
        foreach (var o in scList.S3Objects)
        {
            Console.WriteLine($"  {o.Key} -> StorageClass: {o.StorageClass}");
        }

        // ── 15. ACL ─────────────────────────────────────────────────────
        Console.WriteLine("\n=== ACL ===");
#pragma warning disable CS0618
        var bucketAcl = await client.GetACLAsync(new GetACLRequest { BucketName = BucketName });
#pragma warning restore CS0618
        Console.WriteLine("  Bucket ACL:");
        foreach (var grant in bucketAcl.AccessControlList.Grants)
        {
            Console.WriteLine($"    {grant.Grantee.Type}: {grant.Permission}");
        }

#pragma warning disable CS0618
        await client.PutACLAsync(new PutACLRequest
        {
            BucketName = BucketName,
            Key = "readme.txt",
            CannedACL = S3CannedACL.PublicRead
        });
        Console.WriteLine("  Set readme.txt ACL to public-read");

        var objAcl = await client.GetACLAsync(new GetACLRequest
        {
            BucketName = BucketName,
            Key = "readme.txt"
        });
#pragma warning restore CS0618
        Console.WriteLine("  Object ACL:");
        foreach (var grant in objAcl.AccessControlList.Grants)
        {
            Console.WriteLine($"    {grant.Grantee.Type}: {grant.Permission}");
        }

        // ── 16. Bucket CORS ─────────────────────────────────────────────
        Console.WriteLine("\n=== Bucket CORS ===");
        await client.PutCORSConfigurationAsync(new PutCORSConfigurationRequest
        {
            BucketName = BucketName,
            Configuration = new CORSConfiguration
            {
                Rules =
                [
                    new CORSRule
                    {
                        AllowedOrigins = ["*"],
                        AllowedMethods = ["GET", "PUT", "POST", "DELETE"],
                        AllowedHeaders = ["*"],
                        ExposeHeaders = ["ETag", "x-amz-meta-*"],
                        MaxAgeSeconds = 3600
                    }
                ]
            }
        });
        Console.WriteLine("  CORS configuration set");

        var corsResp = await client.GetCORSConfigurationAsync(
            new GetCORSConfigurationRequest { BucketName = BucketName });
        foreach (var rule in corsResp.Configuration.Rules)
        {
            Console.WriteLine($"  Origins: {string.Join(", ", rule.AllowedOrigins)}");
            Console.WriteLine($"  Methods: {string.Join(", ", rule.AllowedMethods)}");
        }

        await client.DeleteCORSConfigurationAsync(
            new DeleteCORSConfigurationRequest { BucketName = BucketName });
        Console.WriteLine("  CORS configuration deleted");

        // ── 17. Soft Delete ─────────────────────────────────────────────
        Console.WriteLine("\n=== Soft Delete ===");
        const string deleteKey = "delete-test.txt";
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = BucketName,
            Key = deleteKey,
            ContentBody = "temporary content"
        });
        Console.WriteLine($"  Uploaded: {deleteKey}");

        await client.DeleteObjectAsync(BucketName, deleteKey);
        Console.WriteLine($"  Deleted: {deleteKey}");

        try
        {
            await client.GetObjectAsync(BucketName, deleteKey);
            ConsoleHelper.WriteError("  FAIL: expected 404 after delete");
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine("  404 Not Found (as expected)");
        }

        // ── 18. DeleteObjects (bulk delete) ─────────────────────────────
        Console.WriteLine("\n=== Bulk Delete ===");
        var allObjects = await client.ListObjectsV2Async(
            new ListObjectsV2Request { BucketName = BucketName });
        var delResp = await client.DeleteObjectsAsync(new DeleteObjectsRequest
        {
            BucketName = BucketName,
            Objects = allObjects.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
        });
        Console.WriteLine($"  Deleted {delResp.DeletedObjects.Count} objects");

        // ── 19. Cleanup ─────────────────────────────────────────────────
        await client.DeleteBucketAsync(BucketName);
        Console.WriteLine($"  Bucket deleted: {BucketName}");

        // ── 20. Virtual-hosted style access ─────────────────────────────
        Console.WriteLine("\n=== Virtual-Hosted Style ===");
        try
        {
            var vhConfig = new AmazonS3Config
            {
                ServiceURL = "http://s3.localhost:5280",
                ForcePathStyle = false
            };
            using var vhClient = new AmazonS3Client(
                new BasicAWSCredentials("test", "test"), vhConfig);

            const string vhBucket = "vh-demo";
            await vhClient.PutBucketAsync(vhBucket);
            Console.WriteLine($"  Created bucket: {vhBucket}");

            await vhClient.PutObjectAsync(new PutObjectRequest
            {
                BucketName = vhBucket,
                Key = "hello.txt",
                ContentBody = "Hello from virtual-hosted style!"
            });
            Console.WriteLine("  Uploaded hello.txt via virtual-hosted style");

            var vhGet = await vhClient.GetObjectAsync(vhBucket, "hello.txt");
            using (var reader = new StreamReader(vhGet.ResponseStream))
            {
                Console.WriteLine($"  Content: {await reader.ReadToEndAsync()}");
            }

            var vhList = await vhClient.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = vhBucket });
            foreach (var obj in vhList.S3Objects)
            {
                Console.WriteLine($"  Listed: {obj.Key} ({obj.Size} bytes)");
            }

            await vhClient.DeleteObjectAsync(vhBucket, "hello.txt");
            await vhClient.DeleteBucketAsync(vhBucket);
            Console.WriteLine($"  Bucket deleted: {vhBucket}");
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            Console.WriteLine("  [SKIPPED] *.s3.localhost DNS not configured.");
            Console.WriteLine("    To enable, add to hosts file:");
            Console.WriteLine("    127.0.0.1 s3.localhost vh-demo.s3.localhost");
        }

        Console.WriteLine("\n=== All operations completed successfully ===");
    }
}

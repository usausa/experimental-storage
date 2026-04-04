namespace StorageServer.Storage;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

using StorageServer.Storage.Models;

public sealed class StorageService : IStorageService
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private static readonly Random Rng = new();

    private readonly string basePath;
    private readonly string bucketsPath;
    private readonly string multipartPath;
    private readonly StorageOptions options;
    private readonly ILogger<StorageService> logger;
    private readonly ActivitySource activitySource = new("StorageServer.Storage");
    private static readonly Meter StorageMeter = new("StorageServer.Storage");
    private readonly Counter<long> putCounter;
    private readonly Counter<long> getCounter;
    private readonly Counter<long> deleteCounter;

    public StorageService(
        IOptions<StorageOptions> options,
        ILogger<StorageService> logger)
    {
        this.options = options.Value;
        this.logger = logger;

        basePath = Path.GetFullPath(this.options.BasePath);
        bucketsPath = Path.Combine(basePath, "buckets");
        multipartPath = Path.Combine(basePath, "multipart");

        Directory.CreateDirectory(bucketsPath);
        Directory.CreateDirectory(multipartPath);

        putCounter = StorageMeter.CreateCounter<long>("storage.put_objects", "objects", "Number of objects put");
        getCounter = StorageMeter.CreateCounter<long>("storage.get_objects", "objects", "Number of objects retrieved");
        deleteCounter = StorageMeter.CreateCounter<long>("storage.delete_objects", "objects", "Number of objects deleted");
    }

    // ================================================================
    //  Internal JSON metadata model (persisted to disk)
    // ================================================================

    private sealed class StoredObjectMeta
    {
        [JsonPropertyName("contentType")]
        public string ContentType { get; set; } = "application/octet-stream";

        [JsonPropertyName("storageClass")]
        public string StorageClass { get; set; } = "STANDARD";

        [JsonPropertyName("acl")]
        public string Acl { get; set; } = "private";

        [JsonPropertyName("userMetadata")]
        public Dictionary<string, string> UserMetadata { get; set; } = [];

        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; set; } = [];

        [JsonPropertyName("etag")]
        public string? ETag { get; set; }

        [JsonPropertyName("versionId")]
        public string? VersionId { get; set; }
    }

    private sealed class MultipartMeta
    {
        [JsonPropertyName("bucket")]
        public string Bucket { get; set; } = string.Empty;

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("initiated")]
        public DateTimeOffset Initiated { get; set; }
    }

    private sealed class VersionIndex
    {
        [JsonPropertyName("versions")]
        public List<VersionEntry> Versions { get; set; } = [];
    }

    private sealed class VersionEntry
    {
        [JsonPropertyName("versionId")]
        public string VersionId { get; set; } = string.Empty;

        [JsonPropertyName("lastModified")]
        public DateTimeOffset LastModified { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("etag")]
        public string ETag { get; set; } = string.Empty;

        [JsonPropertyName("isDeleteMarker")]
        public bool IsDeleteMarker { get; set; }
    }

    // ================================================================
    //  Bucket operations
    // ================================================================

    public Task<IReadOnlyList<BucketInfo>> ListBucketsAsync(CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("ListBuckets");

        var results = new List<BucketInfo>();

        if (Directory.Exists(bucketsPath))
        {
            foreach (var dir in Directory.GetDirectories(bucketsPath))
            {
                var bucketName = Path.GetFileName(dir);
                var bucketInfoPath = Path.Combine(dir, "meta", "_bucket.json");
                DateTimeOffset created = new DirectoryInfo(dir).CreationTimeUtc;
                if (File.Exists(bucketInfoPath))
                {
                    var json = File.ReadAllText(bucketInfoPath);
                    var stored = JsonSerializer.Deserialize<BucketInfo>(json, JsonOpts);
                    if (stored is not null)
                    {
                        created = stored.CreatedAt;
                    }
                }
                results.Add(new BucketInfo(bucketName, created));
            }
        }

        return Task.FromResult<IReadOnlyList<BucketInfo>>(results);
    }

    public Task CreateBucketAsync(string bucket, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("CreateBucket");
        activity?.SetTag("bucket", bucket);

        ValidateBucketName(bucket);
        var bucketDir = ResolveBucketRootPath(bucket);

        if (Directory.Exists(bucketDir))
        {
            throw new BucketAlreadyExistsException(bucket);
        }

        Directory.CreateDirectory(ResolveBucketDataPath(bucket));
        var bucketMetaDir = ResolveBucketMetaDir(bucket);
        Directory.CreateDirectory(bucketMetaDir);

        var bucketInfo = new BucketInfo(bucket, DateTimeOffset.UtcNow);
        File.WriteAllText(
            Path.Combine(bucketMetaDir, "_bucket.json"),
            JsonSerializer.Serialize(bucketInfo, JsonOpts));

        logger.LogInformation("Created bucket {Bucket}", bucket);
        return Task.CompletedTask;
    }

    public Task<bool> BucketExistsAsync(string bucket, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        return Task.FromResult(Directory.Exists(ResolveBucketDataPath(bucket)));
    }

    public Task DeleteBucketAsync(string bucket, bool force = false, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("DeleteBucket");
        activity?.SetTag("bucket", bucket);

        ValidateBucketName(bucket);
        var bucketDir = ResolveBucketRootPath(bucket);
        if (!Directory.Exists(bucketDir))
        {
            throw new BucketNotFoundException(bucket);
        }
        var dataDir = ResolveBucketDataPath(bucket);
        if (!force && Directory.Exists(dataDir) && Directory.EnumerateFileSystemEntries(dataDir).Any())
        {
            throw new BucketNotEmptyException(bucket);
        }

        Directory.Delete(bucketDir, recursive: true);

        logger.LogInformation("Deleted bucket {Bucket} (force={Force})", bucket, force);
        return Task.CompletedTask;
    }

    public Task<BucketInfo> GetBucketInfoAsync(string bucket, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        var bucketDir = ResolveBucketRootPath(bucket);
        if (!Directory.Exists(bucketDir))
        {
            throw new BucketNotFoundException(bucket);
        }

        var bucketInfoPath = Path.Combine(ResolveBucketMetaDir(bucket), "_bucket.json");
        if (File.Exists(bucketInfoPath))
        {
            var json = File.ReadAllText(bucketInfoPath);
            var info = JsonSerializer.Deserialize<BucketInfo>(json, JsonOpts);
            if (info is not null)
            {
                return Task.FromResult(info);
            }
        }

        var dirInfo = new DirectoryInfo(bucketDir);
        return Task.FromResult(new BucketInfo(bucket, dirInfo.CreationTimeUtc));
    }

    public Task<BucketStats> GetBucketStatsAsync(string bucket, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        var bucketPath = ResolveBucketDataPath(bucket);
        if (!Directory.Exists(bucketPath))
        {
            throw new BucketNotFoundException(bucket);
        }

        long objectCount = 0;
        long totalSize = 0;
        DateTimeOffset? lastModified = null;

        foreach (var file in Directory.EnumerateFiles(bucketPath, "*", SearchOption.AllDirectories))
        {
            var fi = new FileInfo(file);
            objectCount++;
            totalSize += fi.Length;
            var mod = new DateTimeOffset(fi.LastWriteTimeUtc, TimeSpan.Zero);
            if (lastModified is null || mod > lastModified)
            {
                lastModified = mod;
            }
        }

        return Task.FromResult(new BucketStats
        {
            Bucket = bucket,
            ObjectCount = objectCount,
            TotalSizeBytes = totalSize,
            LastModified = lastModified
        });
    }

    // ================================================================
    //  Object operations
    // ================================================================

    public Task<ListObjectsResult> ListObjectsAsync(string bucket, ListObjectsOptions options, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("ListObjects");
        activity?.SetTag("bucket", bucket);

        ValidateBucketName(bucket);
        var bucketPath = ResolveBucketDataPath(bucket);
        if (!Directory.Exists(bucketPath))
        {
            throw new BucketNotFoundException(bucket);
        }

        var prefix = options.Prefix ?? string.Empty;
        var delimiter = options.Delimiter;
        var maxKeys = options.MaxKeys;

        var startAfter = options.StartAfter;
        if (!string.IsNullOrEmpty(options.ContinuationToken))
        {
            startAfter = Encoding.UTF8.GetString(Convert.FromBase64String(options.ContinuationToken));
        }

        IEnumerable<string> allKeys = Directory.GetFiles(bucketPath, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(bucketPath, f).Replace('\\', '/'))
            .Select(DenormalizeFolderMarker)
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(k => k, StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(startAfter))
        {
            allKeys = allKeys.Where(k => string.Compare(k, startAfter, StringComparison.Ordinal) > 0);
        }

        var objects = new List<ObjectSummary>();
        var commonPrefixes = new SortedSet<string>(StringComparer.Ordinal);
        string? lastKey = null;
        var truncated = false;

        foreach (var key in allKeys)
        {
            if (objects.Count + commonPrefixes.Count >= maxKeys)
            {
                truncated = true;
                break;
            }

            if (!string.IsNullOrEmpty(delimiter))
            {
                var remaining = key[prefix.Length..];
                var delimiterIndex = remaining.IndexOf(delimiter, StringComparison.Ordinal);
                if (delimiterIndex >= 0)
                {
                    commonPrefixes.Add(prefix + remaining[..(delimiterIndex + delimiter.Length)]);
                    continue;
                }
            }

            var filePath = ResolveObjectDataPath(bucket, key);
            var info = new FileInfo(filePath);
            var meta = LoadStoredMeta(bucket, key);

            objects.Add(new ObjectSummary
            {
                Key = key,
                Size = info.Length,
                LastModified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                ETag = $"\"{ComputeETag(info)}\"",
                StorageClass = meta?.StorageClass ?? "STANDARD"
            });
            lastKey = key;
        }

        string? nextToken = null;
        if (truncated && lastKey is not null)
        {
            nextToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(lastKey));
        }

        return Task.FromResult(new ListObjectsResult
        {
            Objects = objects,
            CommonPrefixes = commonPrefixes.ToList(),
            IsTruncated = truncated,
            NextContinuationToken = nextToken,
            KeyCount = objects.Count + commonPrefixes.Count
        });
    }

    public Task<ObjectHead> HeadObjectAsync(string bucket, string key, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("HeadObject");

        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);
        if (!File.Exists(filePath))
        {
            throw new ObjectNotFoundException(bucket, key);
        }

        var info = new FileInfo(filePath);
        var meta = LoadStoredMeta(bucket, key);
        var etag = ComputeETag(info);

        return Task.FromResult(new ObjectHead
        {
            Key = key,
            ContentLength = info.Length,
            ContentType = meta?.ContentType ?? ResolveContentType(key),
            ETag = $"\"{etag}\"",
            LastModified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            StorageClass = meta?.StorageClass ?? "STANDARD",
            Acl = meta?.Acl ?? "private",
            VersionId = meta?.VersionId,
            UserMetadata = meta?.UserMetadata ?? []
        });
    }

    public async Task<ObjectData> GetObjectAsync(string bucket, string key, GetObjectOptions? options = null, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("GetObject");
        getCounter.Add(1);

        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);
        if (!File.Exists(filePath))
        {
            throw new ObjectNotFoundException(bucket, key);
        }

        var info = new FileInfo(filePath);
        var etag = ComputeETag(info);
        var lastModified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);

        if (options is not null)
        {
            EvaluateConditionalHeaders(etag, lastModified, options);
        }

        var meta = LoadStoredMeta(bucket, key);

        Stream content;
        long contentLength;

        if (options?.RangeStart is not null || options?.RangeEnd is not null)
        {
            var start = options.RangeStart ?? 0;
            var end = options.RangeEnd ?? (info.Length - 1);
            contentLength = end - start + 1;

            var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            fs.Seek(start, SeekOrigin.Begin);
            content = new BoundedStream(fs, contentLength);
        }
        else
        {
            contentLength = info.Length;
            content = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        }

        var head = new ObjectHead
        {
            Key = key,
            ContentLength = contentLength,
            ContentType = meta?.ContentType ?? ResolveContentType(key),
            ETag = $"\"{etag}\"",
            LastModified = lastModified,
            StorageClass = meta?.StorageClass ?? "STANDARD",
            Acl = meta?.Acl ?? "private",
            VersionId = meta?.VersionId,
            UserMetadata = meta?.UserMetadata ?? []
        };

        return new ObjectData { Head = head, Content = content };
    }

    public async Task<PutObjectResult> PutObjectAsync(string bucket, string key, Stream data, PutObjectOptions? options = null, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("PutObject");
        putCounter.Add(1);

        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);
        var versionId = GenerateVersionId();

        if (File.Exists(filePath))
        {
            await ArchiveCurrentVersionAsync(bucket, key, filePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await data.CopyToAsync(fs, ct);
        }

        var info = new FileInfo(filePath);
        var etag = ComputeETag(info);

        var storedMeta = new StoredObjectMeta
        {
            ContentType = options?.ContentType ?? ResolveContentType(key),
            StorageClass = options?.StorageClass ?? "STANDARD",
            Acl = options?.Acl ?? "private",
            UserMetadata = options?.UserMetadata ?? [],
            Tags = options?.Tags ?? [],
            ETag = $"\"{etag}\"",
            VersionId = versionId
        };
        SaveStoredMeta(bucket, key, storedMeta);

        logger.LogDebug("Put object {Bucket}/{Key} versionId={VersionId}", bucket, key, versionId);

        return new PutObjectResult
        {
            ETag = $"\"{etag}\"",
            VersionId = versionId
        };
    }

    public async Task DeleteObjectAsync(string bucket, string key, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("DeleteObject");
        deleteCounter.Add(1);

        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);

        if (File.Exists(filePath))
        {
            await ArchiveCurrentVersionAsync(bucket, key, filePath, isDeleteMarker: true);

            File.Delete(filePath);
            CleanEmptyDirectories(filePath, ResolveBucketDataPath(bucket));
        }

        DeleteStoredMeta(bucket, key);

        logger.LogDebug("Deleted object {Bucket}/{Key}", bucket, key);
    }

    public async Task<IReadOnlyList<DeleteObjectResult>> DeleteObjectsAsync(string bucket, IEnumerable<string> keys, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("DeleteObjects");

        ValidateBucketName(bucket);
        EnsureBucketExists(bucket);

        var results = new List<DeleteObjectResult>();
        foreach (var key in keys)
        {
            try
            {
                await DeleteObjectAsync(bucket, key, ct);
                results.Add(new DeleteObjectResult
                {
                    Key = key,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                results.Add(new DeleteObjectResult
                {
                    Key = key,
                    Success = false,
                    ErrorCode = "InternalError",
                    ErrorMessage = ex.Message
                });
            }
        }

        return results;
    }

    public async Task<CopyObjectResult> CopyObjectAsync(string bucket, string key, string sourceBucket, string sourceKey, CopyObjectOptions? options = null, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("CopyObject");

        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        ValidateBucketName(sourceBucket);
        ValidateObjectKey(sourceKey);
        EnsureBucketExists(bucket);
        EnsureBucketExists(sourceBucket);

        var srcFilePath = ResolveObjectDataPath(sourceBucket, sourceKey);
        if (!File.Exists(srcFilePath))
        {
            throw new ObjectNotFoundException(sourceBucket, sourceKey);
        }

        var destFilePath = ResolveObjectDataPath(bucket, key);
        var versionId = GenerateVersionId();

        if (File.Exists(destFilePath))
        {
            await ArchiveCurrentVersionAsync(bucket, key, destFilePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)!);
        File.Copy(srcFilePath, destFilePath, overwrite: true);

        StoredObjectMeta destMeta;
        var directive = options?.MetadataDirective ?? "COPY";

        if (string.Equals(directive, "REPLACE", StringComparison.OrdinalIgnoreCase) && options?.NewMetadata is not null)
        {
            var nm = options.NewMetadata;
            destMeta = new StoredObjectMeta
            {
                ContentType = nm.ContentType ?? ResolveContentType(key),
                StorageClass = nm.StorageClass ?? "STANDARD",
                Acl = nm.Acl ?? "private",
                UserMetadata = nm.UserMetadata ?? [],
                Tags = nm.Tags ?? [],
                VersionId = versionId
            };
        }
        else
        {
            var srcMeta = LoadStoredMeta(sourceBucket, sourceKey);
            destMeta = new StoredObjectMeta
            {
                ContentType = srcMeta?.ContentType ?? ResolveContentType(sourceKey),
                StorageClass = srcMeta?.StorageClass ?? "STANDARD",
                Acl = srcMeta?.Acl ?? "private",
                UserMetadata = srcMeta?.UserMetadata ?? [],
                Tags = srcMeta?.Tags ?? [],
                VersionId = versionId
            };
        }

        var info = new FileInfo(destFilePath);
        var etag = ComputeETag(info);
        destMeta.ETag = $"\"{etag}\"";
        SaveStoredMeta(bucket, key, destMeta);

        return new CopyObjectResult
        {
            ETag = $"\"{etag}\"",
            LastModified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            VersionId = versionId
        };
    }

    // ================================================================
    //  Metadata operations
    // ================================================================

    public Task<ObjectMetadata> GetObjectMetadataAsync(string bucket, string key, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);
        if (!File.Exists(filePath))
        {
            throw new ObjectNotFoundException(bucket, key);
        }

        var info = new FileInfo(filePath);
        var meta = LoadStoredMeta(bucket, key);
        var etag = ComputeETag(info);

        return Task.FromResult(new ObjectMetadata
        {
            Key = key,
            ContentType = meta?.ContentType ?? ResolveContentType(key),
            ContentLength = info.Length,
            ETag = $"\"{etag}\"",
            LastModified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
            StorageClass = meta?.StorageClass ?? "STANDARD",
            Acl = meta?.Acl ?? "private",
            VersionId = meta?.VersionId,
            UserMetadata = meta?.UserMetadata ?? [],
            Tags = meta?.Tags ?? []
        });
    }

    public Task UpdateObjectMetadataAsync(string bucket, string key, ObjectMetadataPatch patch, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);
        if (!File.Exists(filePath))
        {
            throw new ObjectNotFoundException(bucket, key);
        }

        var meta = LoadStoredMeta(bucket, key) ?? new StoredObjectMeta
        {
            ContentType = ResolveContentType(key)
        };

        if (patch.Acl is not null)
        {
            meta.Acl = patch.Acl;
        }

        if (patch.UserMetadata is not null)
        {
            meta.UserMetadata = patch.UserMetadata;
        }

        if (patch.Tags is not null)
        {
            meta.Tags = patch.Tags;
        }

        SaveStoredMeta(bucket, key, meta);
        return Task.CompletedTask;
    }

    // ================================================================
    //  Tag operations - Object
    // ================================================================

    public Task<Dictionary<string, string>> GetObjectTagsAsync(string bucket, string key, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);
        if (!File.Exists(filePath))
        {
            throw new ObjectNotFoundException(bucket, key);
        }

        var meta = LoadStoredMeta(bucket, key);
        return Task.FromResult(meta?.Tags ?? []);
    }

    public Task PutObjectTagsAsync(string bucket, string key, Dictionary<string, string> tags, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);
        if (!File.Exists(filePath))
        {
            throw new ObjectNotFoundException(bucket, key);
        }

        var meta = LoadStoredMeta(bucket, key) ?? new StoredObjectMeta
        {
            ContentType = ResolveContentType(key)
        };
        meta.Tags = tags;
        SaveStoredMeta(bucket, key, meta);
        return Task.CompletedTask;
    }

    public Task DeleteObjectTagsAsync(string bucket, string key, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);
        if (!File.Exists(filePath))
        {
            throw new ObjectNotFoundException(bucket, key);
        }

        var meta = LoadStoredMeta(bucket, key);
        if (meta is not null && meta.Tags.Count > 0)
        {
            meta.Tags = [];
            SaveStoredMeta(bucket, key, meta);
        }

        return Task.CompletedTask;
    }

    // ================================================================
    //  Tag operations - Bucket
    // ================================================================

    public Task<Dictionary<string, string>> GetBucketTagsAsync(string bucket, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        EnsureBucketExists(bucket);

        var path = ResolveBucketMetaFile(bucket, "_tags.json");
        if (!File.Exists(path))
        {
            return Task.FromResult(new Dictionary<string, string>());
        }

        var json = File.ReadAllText(path);
        var tags = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOpts) ?? [];
        return Task.FromResult(tags);
    }

    public Task PutBucketTagsAsync(string bucket, Dictionary<string, string> tags, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        EnsureBucketExists(bucket);

        var path = ResolveBucketMetaFile(bucket, "_tags.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(tags, JsonOpts));
        return Task.CompletedTask;
    }

    public Task DeleteBucketTagsAsync(string bucket, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        EnsureBucketExists(bucket);

        var path = ResolveBucketMetaFile(bucket, "_tags.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    // ================================================================

    public Task<string> GetObjectAclAsync(string bucket, string key, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);
        if (!File.Exists(filePath))
        {
            throw new ObjectNotFoundException(bucket, key);
        }

        var meta = LoadStoredMeta(bucket, key);
        return Task.FromResult(meta?.Acl ?? "private");
    }

    public Task PutObjectAclAsync(string bucket, string key, string acl, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);
        if (!File.Exists(filePath))
        {
            throw new ObjectNotFoundException(bucket, key);
        }

        var meta = LoadStoredMeta(bucket, key) ?? new StoredObjectMeta
        {
            ContentType = ResolveContentType(key)
        };
        meta.Acl = acl;
        SaveStoredMeta(bucket, key, meta);
        return Task.CompletedTask;
    }

    // ================================================================
    //  ACL operations - Bucket
    // ================================================================

    public Task<string> GetBucketAclAsync(string bucket, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        EnsureBucketExists(bucket);

        var path = ResolveBucketMetaFile(bucket, "_acl.json");
        if (!File.Exists(path))
        {
            return Task.FromResult("private");
        }

        var json = File.ReadAllText(path);
        var acl = JsonSerializer.Deserialize<string>(json, JsonOpts) ?? "private";
        return Task.FromResult(acl);
    }

    public Task PutBucketAclAsync(string bucket, string acl, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        EnsureBucketExists(bucket);

        var path = ResolveBucketMetaFile(bucket, "_acl.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(acl, JsonOpts));
        return Task.CompletedTask;
    }

    // ================================================================
    //  CORS operations
    // ================================================================

    public Task<IReadOnlyList<CorsRule>> GetBucketCorsAsync(string bucket, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        EnsureBucketExists(bucket);

        var path = ResolveBucketMetaFile(bucket, "_cors.json");
        if (!File.Exists(path))
        {
            throw new CorsConfigNotFoundException(bucket);
        }

        var json = File.ReadAllText(path);
        var rules = JsonSerializer.Deserialize<List<CorsRule>>(json, JsonOpts) ?? [];
        if (rules.Count == 0)
        {
            throw new CorsConfigNotFoundException(bucket);
        }

        return Task.FromResult<IReadOnlyList<CorsRule>>(rules);
    }

    public Task PutBucketCorsAsync(string bucket, IReadOnlyList<CorsRule> rules, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        EnsureBucketExists(bucket);

        var path = ResolveBucketMetaFile(bucket, "_cors.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(rules, JsonOpts));
        return Task.CompletedTask;
    }

    public Task DeleteBucketCorsAsync(string bucket, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        EnsureBucketExists(bucket);

        var path = ResolveBucketMetaFile(bucket, "_cors.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    // ================================================================
    //  Multipart upload operations
    // ================================================================

    public Task<string> CreateMultipartUploadAsync(string bucket, string key, PutObjectOptions? options = null, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("CreateMultipartUpload");

        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var uploadId = Guid.NewGuid().ToString("N");
        var uploadDir = Path.Combine(multipartPath, uploadId);
        Directory.CreateDirectory(uploadDir);

        var info = new MultipartMeta
        {
            Bucket = bucket,
            Key = key,
            Initiated = DateTimeOffset.UtcNow
        };
        File.WriteAllText(
            Path.Combine(uploadDir, "_info.json"),
            JsonSerializer.Serialize(info, JsonOpts));

        var meta = new StoredObjectMeta
        {
            ContentType = options?.ContentType ?? ResolveContentType(key),
            StorageClass = options?.StorageClass ?? "STANDARD",
            Acl = options?.Acl ?? "private",
            UserMetadata = options?.UserMetadata ?? [],
            Tags = options?.Tags ?? []
        };
        File.WriteAllText(
            Path.Combine(uploadDir, "_meta.json"),
            JsonSerializer.Serialize(meta, JsonOpts));

        logger.LogDebug("Created multipart upload {UploadId} for {Bucket}/{Key}", uploadId, bucket, key);
        return Task.FromResult(uploadId);
    }

    public async Task<UploadPartResult> UploadPartAsync(string uploadId, int partNumber, Stream data, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("UploadPart");

        var uploadDir = Path.Combine(multipartPath, uploadId);
        if (!Directory.Exists(uploadDir))
        {
            throw new MultipartUploadNotFoundException(uploadId);
        }

        var partPath = Path.Combine(uploadDir, $"{partNumber}.part");

        await using (var fs = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await data.CopyToAsync(fs, ct);
        }

        var etag = ComputeETag(new FileInfo(partPath));
        return new UploadPartResult
        {
            ETag = $"\"{etag}\""
        };
    }

    public async Task<CompleteMultipartResult> CompleteMultipartUploadAsync(string uploadId, IEnumerable<PartInfo> parts, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("CompleteMultipartUpload");

        var uploadDir = Path.Combine(multipartPath, uploadId);
        if (!Directory.Exists(uploadDir))
        {
            throw new MultipartUploadNotFoundException(uploadId);
        }

        var infoPath = Path.Combine(uploadDir, "_info.json");
        if (!File.Exists(infoPath))
        {
            throw new MultipartUploadNotFoundException(uploadId);
        }

        var infoJson = await File.ReadAllTextAsync(infoPath, ct);
        var uploadInfo = JsonSerializer.Deserialize<MultipartMeta>(infoJson, JsonOpts)
            ?? throw new MultipartUploadNotFoundException(uploadId);

        var bucket = uploadInfo.Bucket;
        var key = uploadInfo.Key;
        EnsureBucketExists(bucket);

        var filePath = ResolveObjectDataPath(bucket, key);

        if (File.Exists(filePath))
        {
            await ArchiveCurrentVersionAsync(bucket, key, filePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var partNumbers = parts.Select(p => p.PartNumber).OrderBy(n => n).ToList();
        var partMd5s = new List<byte[]>();

        await using (var output = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            foreach (var partNumber in partNumbers)
            {
                var partPath = Path.Combine(uploadDir, $"{partNumber}.part");
                if (!File.Exists(partPath))
                {
                    throw new StorageException("InvalidPart", 400, $"Part {partNumber} not found.");
                }

                var partHash = MD5.HashData(await File.ReadAllBytesAsync(partPath, ct));
                partMd5s.Add(partHash);

                await using var partStream = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                await partStream.CopyToAsync(output, ct);
            }
        }

        var concatenated = partMd5s.SelectMany(h => h).ToArray();
        var compositeHash = MD5.HashData(concatenated);
        var compositeEtag = $"\"{Convert.ToHexStringLower(compositeHash)}-{partNumbers.Count}\"";

        var versionId = GenerateVersionId();

        var objMetaPath = Path.Combine(uploadDir, "_meta.json");
        StoredObjectMeta? objMeta = null;
        if (File.Exists(objMetaPath))
        {
            var metaJson = await File.ReadAllTextAsync(objMetaPath, ct);
            objMeta = JsonSerializer.Deserialize<StoredObjectMeta>(metaJson, JsonOpts);
        }
        objMeta ??= new StoredObjectMeta { ContentType = ResolveContentType(key) };
        objMeta.ETag = compositeEtag;
        objMeta.VersionId = versionId;
        SaveStoredMeta(bucket, key, objMeta);

        Directory.Delete(uploadDir, recursive: true);

        logger.LogDebug("Completed multipart upload {UploadId} for {Bucket}/{Key}", uploadId, bucket, key);

        return new CompleteMultipartResult
        {
            Bucket = bucket,
            Key = key,
            ETag = compositeEtag,
            VersionId = versionId
        };
    }

    public Task AbortMultipartUploadAsync(string uploadId, CancellationToken ct = default)
    {
        var uploadDir = Path.Combine(multipartPath, uploadId);
        if (Directory.Exists(uploadDir))
        {
            Directory.Delete(uploadDir, recursive: true);
        }

        logger.LogDebug("Aborted multipart upload {UploadId}", uploadId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MultipartUploadInfo>> ListMultipartUploadsAsync(string bucket, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        EnsureBucketExists(bucket);

        var results = new List<MultipartUploadInfo>();

        if (Directory.Exists(multipartPath))
        {
            foreach (var dir in Directory.GetDirectories(multipartPath))
            {
                var infoPath = Path.Combine(dir, "_info.json");
                if (!File.Exists(infoPath))
                {
                    continue;
                }

                var json = File.ReadAllText(infoPath);
                var info = JsonSerializer.Deserialize<MultipartMeta>(json, JsonOpts);
                if (info is null || info.Bucket != bucket)
                {
                    continue;
                }

                results.Add(new MultipartUploadInfo
                {
                    UploadId = Path.GetFileName(dir),
                    Bucket = info.Bucket,
                    Key = info.Key,
                    Initiated = info.Initiated
                });
            }
        }

        return Task.FromResult<IReadOnlyList<MultipartUploadInfo>>(results);
    }

    public Task<IReadOnlyList<PartInfo>> ListPartsAsync(string uploadId, CancellationToken ct = default)
    {
        var uploadDir = Path.Combine(multipartPath, uploadId);
        if (!Directory.Exists(uploadDir))
        {
            throw new MultipartUploadNotFoundException(uploadId);
        }

        var results = Directory.GetFiles(uploadDir, "*.part")
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                if (!int.TryParse(name, out var partNumber))
                {
                    return null;
                }

                var fi = new FileInfo(f);
                return new PartInfo
                {
                    PartNumber = partNumber,
                    Size = fi.Length,
                    ETag = $"\"{ComputeETag(fi)}\"",
                    LastModified = new DateTimeOffset(fi.LastWriteTimeUtc, TimeSpan.Zero)
                };
            })
            .Where(p => p is not null)
            .OrderBy(p => p!.PartNumber)
            .Cast<PartInfo>()
            .ToList();

        return Task.FromResult<IReadOnlyList<PartInfo>>(results);
    }

    // ================================================================
    //  Version operations
    // ================================================================

    public Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(string bucket, string key, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var index = LoadVersionIndex(bucket, key);
        var results = new List<VersionInfo>();

        var filePath = ResolveObjectDataPath(bucket, key);
        if (File.Exists(filePath))
        {
            var fi = new FileInfo(filePath);
            var meta = LoadStoredMeta(bucket, key);
            results.Add(new VersionInfo
            {
                VersionId = meta?.VersionId ?? "current",
                LastModified = new DateTimeOffset(fi.LastWriteTimeUtc, TimeSpan.Zero),
                Size = fi.Length,
                ETag = $"\"{ComputeETag(fi)}\"",
                IsCurrent = true,
                IsDeleteMarker = false
            });
        }

        foreach (var entry in index.Versions.OrderByDescending(v => v.LastModified))
        {
            results.Add(new VersionInfo
            {
                VersionId = entry.VersionId,
                LastModified = entry.LastModified,
                Size = entry.Size,
                ETag = entry.ETag,
                IsCurrent = false,
                IsDeleteMarker = entry.IsDeleteMarker
            });
        }

        return Task.FromResult<IReadOnlyList<VersionInfo>>(results);
    }

    public Task<ObjectData> GetObjectVersionAsync(string bucket, string key, string versionId, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("GetObjectVersion");

        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var meta = LoadStoredMeta(bucket, key);
        if (meta?.VersionId == versionId)
        {
            return GetObjectAsync(bucket, key, ct: ct);
        }

        var index = LoadVersionIndex(bucket, key);
        var entry = index.Versions.FirstOrDefault(v => v.VersionId == versionId)
            ?? throw new VersionNotFoundException(bucket, key, versionId);

        if (entry.IsDeleteMarker)
        {
            throw new ObjectNotFoundException(bucket, key);
        }

        var versionDataPath = ResolveVersionDataPath(bucket, key, versionId);
        if (!File.Exists(versionDataPath))
        {
            throw new VersionNotFoundException(bucket, key, versionId);
        }

        var versionMetaPath = ResolveVersionMetaPath(bucket, key, versionId);
        StoredObjectMeta? versionMeta = null;
        if (File.Exists(versionMetaPath))
        {
            var json = File.ReadAllText(versionMetaPath);
            versionMeta = JsonSerializer.Deserialize<StoredObjectMeta>(json, JsonOpts);
        }

        var fi = new FileInfo(versionDataPath);
        var content = new FileStream(versionDataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

        var head = new ObjectHead
        {
            Key = key,
            ContentLength = fi.Length,
            ContentType = versionMeta?.ContentType ?? ResolveContentType(key),
            ETag = entry.ETag,
            LastModified = entry.LastModified,
            StorageClass = versionMeta?.StorageClass ?? "STANDARD",
            Acl = versionMeta?.Acl ?? "private",
            VersionId = versionId,
            UserMetadata = versionMeta?.UserMetadata ?? []
        };

        return Task.FromResult(new ObjectData { Head = head, Content = content });
    }

    public async Task RestoreVersionAsync(string bucket, string key, string versionId, CancellationToken ct = default)
    {
        using var activity = activitySource.StartActivity("RestoreVersion");

        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var index = LoadVersionIndex(bucket, key);
        var entry = index.Versions.FirstOrDefault(v => v.VersionId == versionId)
            ?? throw new VersionNotFoundException(bucket, key, versionId);

        if (entry.IsDeleteMarker)
        {
            throw new StorageException("InvalidRequest", 400, "Cannot restore a delete marker.");
        }

        var versionDataPath = ResolveVersionDataPath(bucket, key, versionId);
        if (!File.Exists(versionDataPath))
        {
            throw new VersionNotFoundException(bucket, key, versionId);
        }

        var filePath = ResolveObjectDataPath(bucket, key);

        if (File.Exists(filePath))
        {
            await ArchiveCurrentVersionAsync(bucket, key, filePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.Copy(versionDataPath, filePath, overwrite: true);

        var versionMetaPath = ResolveVersionMetaPath(bucket, key, versionId);
        if (File.Exists(versionMetaPath))
        {
            var json = File.ReadAllText(versionMetaPath);
            var versionMeta = JsonSerializer.Deserialize<StoredObjectMeta>(json, JsonOpts);
            if (versionMeta is not null)
            {
                versionMeta.VersionId = GenerateVersionId();
                SaveStoredMeta(bucket, key, versionMeta);
            }
        }

        logger.LogInformation("Restored version {VersionId} of {Bucket}/{Key}", versionId, bucket, key);
    }

    public Task DeleteVersionAsync(string bucket, string key, string versionId, CancellationToken ct = default)
    {
        ValidateBucketName(bucket);
        ValidateObjectKey(key);
        EnsureBucketExists(bucket);

        var index = LoadVersionIndex(bucket, key);
        var entry = index.Versions.FirstOrDefault(v => v.VersionId == versionId)
            ?? throw new VersionNotFoundException(bucket, key, versionId);

        var versionDataPath = ResolveVersionDataPath(bucket, key, versionId);
        if (File.Exists(versionDataPath))
        {
            File.Delete(versionDataPath);
        }

        var versionMetaPath = ResolveVersionMetaPath(bucket, key, versionId);
        if (File.Exists(versionMetaPath))
        {
            File.Delete(versionMetaPath);
        }

        index.Versions.Remove(entry);
        SaveVersionIndex(bucket, key, index);

        var versionDir = ResolveVersionDir(bucket, key);
        if (Directory.Exists(versionDir) && !Directory.EnumerateFileSystemEntries(versionDir).Any())
        {
            Directory.Delete(versionDir);
        }

        return Task.CompletedTask;
    }

    // ================================================================
    //  Thumbnail
    // ================================================================

    public Task<Stream?> GetThumbnailAsync(string bucket, string key, int maxWidth = 128, int maxHeight = 128, CancellationToken ct = default)
    {
        return Task.FromResult<Stream?>(null);
    }

    // ================================================================
    //  Path resolution helpers
    // ================================================================

    private string ResolveBucketRootPath(string bucket)
    {
        var path = Path.GetFullPath(Path.Combine(bucketsPath, bucket));
        if (!path.StartsWith(bucketsPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidBucketNameException(bucket, "Path traversal detected.");
        }
        return path;
    }

    private string ResolveBucketDataPath(string bucket)
    {
        return Path.Combine(ResolveBucketRootPath(bucket), "data");
    }

    private string ResolveBucketMetaDir(string bucket)
    {
        return Path.Combine(ResolveBucketRootPath(bucket), "meta");
    }

    private string ResolveBucketVersionsDir(string bucket)
    {
        return Path.Combine(ResolveBucketRootPath(bucket), "versions");
    }

    private const string FolderMarkerFile = ".folder";

    private static string NormalizeKeyForPath(string key)
    {
        var normalized = key.Replace('/', Path.DirectorySeparatorChar);
        if (normalized.EndsWith(Path.DirectorySeparatorChar))
        {
            normalized += FolderMarkerFile;
        }
        return normalized;
    }

    private static string DenormalizeFolderMarker(string key)
    {
        if (key.EndsWith("/" + FolderMarkerFile, StringComparison.Ordinal))
        {
            return key[..^FolderMarkerFile.Length];
        }

        if (key == FolderMarkerFile)
        {
            return "/";
        }
        return key;
    }

    private string ResolveObjectDataPath(string bucket, string key)
    {
        var bucketDataPath = ResolveBucketDataPath(bucket);
        var normalizedKey = NormalizeKeyForPath(key);
        var path = Path.GetFullPath(Path.Combine(bucketDataPath, normalizedKey));
        if (!path.StartsWith(bucketDataPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidObjectKeyException(key, "Path traversal detected.");
        }
        return path;
    }

    private string ResolveObjectMetaPath(string bucket, string key)
    {
        var normalizedKey = NormalizeKeyForPath(key);
        var bucketMetaDir = ResolveBucketMetaDir(bucket);
        var path = Path.GetFullPath(
            Path.Combine(bucketMetaDir, "objects", normalizedKey + ".meta.json"));
        if (!path.StartsWith(bucketMetaDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidObjectKeyException(key, "Path traversal detected.");
        }
        return path;
    }

    private string ResolveBucketMetaFile(string bucket, string filename)
    {
        return Path.Combine(ResolveBucketMetaDir(bucket), filename);
    }

    private string ResolveVersionDir(string bucket, string key)
    {
        var normalizedKey = NormalizeKeyForPath(key);
        var keyDir = Path.GetDirectoryName(normalizedKey) ?? string.Empty;
        var fileName = Path.GetFileName(normalizedKey);
        return Path.Combine(ResolveBucketVersionsDir(bucket), keyDir, fileName);
    }

    private string ResolveVersionDataPath(string bucket, string key, string versionId)
    {
        return Path.Combine(ResolveVersionDir(bucket, key), $"{versionId}.data");
    }

    private string ResolveVersionMetaPath(string bucket, string key, string versionId)
    {
        return Path.Combine(ResolveVersionDir(bucket, key), $"{versionId}.meta.json");
    }

    private string ResolveVersionIndexPath(string bucket, string key)
    {
        return Path.Combine(ResolveVersionDir(bucket, key), "_versions.json");
    }

    // ================================================================
    //  Object metadata persistence
    // ================================================================

    private void SaveStoredMeta(string bucket, string key, StoredObjectMeta meta)
    {
        var path = ResolveObjectMetaPath(bucket, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(meta, JsonOpts));
    }

    private StoredObjectMeta? LoadStoredMeta(string bucket, string key)
    {
        var path = ResolveObjectMetaPath(bucket, key);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<StoredObjectMeta>(json, JsonOpts);
    }

    private void DeleteStoredMeta(string bucket, string key)
    {
        var path = ResolveObjectMetaPath(bucket, key);
        if (!File.Exists(path))
        {
            return;
        }

        File.Delete(path);
        var objectsDir = Path.Combine(ResolveBucketMetaDir(bucket), "objects");
        CleanEmptyDirectories(path, objectsDir);
    }

    // ================================================================
    //  Version index persistence
    // ================================================================

    private VersionIndex LoadVersionIndex(string bucket, string key)
    {
        var path = ResolveVersionIndexPath(bucket, key);
        if (!File.Exists(path))
        {
            return new VersionIndex();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<VersionIndex>(json, JsonOpts) ?? new VersionIndex();
    }

    private void SaveVersionIndex(string bucket, string key, VersionIndex index)
    {
        var path = ResolveVersionIndexPath(bucket, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(index, JsonOpts));
    }

    private async Task ArchiveCurrentVersionAsync(string bucket, string key, string currentFilePath, bool isDeleteMarker = false)
    {
        var versionId = GenerateVersionId();
        var versionDir = ResolveVersionDir(bucket, key);
        Directory.CreateDirectory(versionDir);

        if (!isDeleteMarker)
        {
            var versionDataPath = Path.Combine(versionDir, $"{versionId}.data");
            File.Copy(currentFilePath, versionDataPath, overwrite: true);

            var currentMeta = LoadStoredMeta(bucket, key);
            if (currentMeta is not null)
            {
                var versionMetaPath = Path.Combine(versionDir, $"{versionId}.meta.json");
                File.WriteAllText(versionMetaPath, JsonSerializer.Serialize(currentMeta, JsonOpts));
            }
        }

        var fi = new FileInfo(currentFilePath);
        var index = LoadVersionIndex(bucket, key);
        index.Versions.Add(new VersionEntry
        {
            VersionId = versionId,
            LastModified = new DateTimeOffset(fi.LastWriteTimeUtc, TimeSpan.Zero),
            Size = isDeleteMarker ? 0 : fi.Length,
            ETag = isDeleteMarker ? string.Empty : $"\"{ComputeETag(fi)}\"",
            IsDeleteMarker = isDeleteMarker
        });

        if (options.MaxVersionsPerObject > 0)
        {
            var nonDeleteVersions = index.Versions.Where(v => !v.IsDeleteMarker)
                .OrderByDescending(v => v.LastModified).ToList();
            while (nonDeleteVersions.Count > options.MaxVersionsPerObject)
            {
                var oldest = nonDeleteVersions[^1];
                nonDeleteVersions.RemoveAt(nonDeleteVersions.Count - 1);
                index.Versions.Remove(oldest);

                var oldDataPath = Path.Combine(versionDir, $"{oldest.VersionId}.data");
                if (File.Exists(oldDataPath))
                {
                    File.Delete(oldDataPath);
                }

                var oldMetaPath = Path.Combine(versionDir, $"{oldest.VersionId}.meta.json");
                if (File.Exists(oldMetaPath))
                {
                    File.Delete(oldMetaPath);
                }
            }
        }

        if (options.VersionRetentionDays > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-options.VersionRetentionDays);
            var expired = index.Versions.Where(v => v.LastModified < cutoff).ToList();
            foreach (var old in expired)
            {
                index.Versions.Remove(old);
                var oldDataPath = Path.Combine(versionDir, $"{old.VersionId}.data");
                if (File.Exists(oldDataPath))
                {
                    File.Delete(oldDataPath);
                }

                var oldMetaPath = Path.Combine(versionDir, $"{old.VersionId}.meta.json");
                if (File.Exists(oldMetaPath))
                {
                    File.Delete(oldMetaPath);
                }
            }
        }

        SaveVersionIndex(bucket, key, index);
    }

    // ================================================================
    //  Validation
    // ================================================================

    private static void ValidateBucketName(string bucket)
    {
        ArgumentNullException.ThrowIfNull(bucket);
        if (string.IsNullOrWhiteSpace(bucket))
        {
            throw new InvalidBucketNameException(bucket, "Bucket name cannot be empty.");
        }

        if (bucket.StartsWith('.'))
        {
            throw new InvalidBucketNameException(bucket, "Bucket name cannot start with a dot.");
        }

        if (bucket.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidBucketNameException(bucket, "Bucket name cannot contain '..'.");
        }

        if (bucket.Contains('/'))
        {
            throw new InvalidBucketNameException(bucket, "Bucket name cannot contain '/'.");
        }

        if (bucket.Contains('\\'))
        {
            throw new InvalidBucketNameException(bucket, "Bucket name cannot contain '\\'.");
        }
    }

    private static void ValidateObjectKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidObjectKeyException(key, "Object key cannot be empty.");
        }

        if (key.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidObjectKeyException(key, "Object key cannot contain '..'.");
        }
    }

    private void EnsureBucketExists(string bucket)
    {
        var bucketDir = ResolveBucketRootPath(bucket);
        if (!Directory.Exists(bucketDir))
        {
            throw new BucketNotFoundException(bucket);
        }
    }

    // ================================================================

    private static string ComputeETag(FileInfo info)
    {
        using var stream = info.OpenRead();
        var hash = MD5.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    private static string ResolveContentType(string key) =>
        ContentTypeProvider.TryGetContentType(key, out var ct) ? ct : "application/octet-stream";

    private static string GenerateVersionId()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var randomPart = new char[6];
        lock (Rng)
        {
            for (var i = 0; i < 6; i++)
            {
                randomPart[i] = chars[Rng.Next(chars.Length)];
            }
        }
        return $"v_{timestamp}_{new string(randomPart)}";
    }

    private static void CleanEmptyDirectories(string filePath, string stopAtPath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (dir is not null
            && dir.Length > stopAtPath.Length
            && dir.StartsWith(stopAtPath, StringComparison.OrdinalIgnoreCase)
            && !Directory.EnumerateFileSystemEntries(dir).Any())
        {
            Directory.Delete(dir);
            dir = Path.GetDirectoryName(dir);
        }
    }

    private static void EvaluateConditionalHeaders(string etag, DateTimeOffset lastModified, GetObjectOptions options)
    {
        var quotedEtag = $"\"{etag}\"";

        if (!string.IsNullOrEmpty(options.IfMatch) && options.IfMatch != "*" && options.IfMatch != quotedEtag)
        {
            throw new PreconditionFailedException($"ETag '{quotedEtag}' does not match If-Match '{options.IfMatch}'.");
        }

        if (options.IfUnmodifiedSince.HasValue && lastModified > options.IfUnmodifiedSince.Value)
        {
            throw new PreconditionFailedException("Object was modified after If-Unmodified-Since.");
        }

        if (!string.IsNullOrEmpty(options.IfNoneMatch)
            && (options.IfNoneMatch == "*" || options.IfNoneMatch == quotedEtag))
        {
            throw new NotModifiedException();
        }

        if (options.IfModifiedSince.HasValue && lastModified <= options.IfModifiedSince.Value)
        {
            throw new NotModifiedException();
        }
    }

    // ================================================================
    //  BoundedStream - for range requests
    // ================================================================

    private sealed class BoundedStream : Stream
    {
        private readonly Stream inner;
        private long remaining;

        public BoundedStream(Stream inner, long length)
        {
            this.inner = inner;
            remaining = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (remaining <= 0)
            {
                return 0;
            }

            var toRead = (int)Math.Min(count, remaining);
            var read = inner.Read(buffer, offset, toRead);
            remaining -= read;
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (remaining <= 0)
            {
                return 0;
            }

            var toRead = (int)Math.Min(count, remaining);
            var read = await inner.ReadAsync(buffer.AsMemory(offset, toRead), cancellationToken);
            remaining -= read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (remaining <= 0)
            {
                return 0;
            }

            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await inner.ReadAsync(buffer[..toRead], cancellationToken);
            remaining -= read;
            return read;
        }

        public override void Flush()
        {
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}

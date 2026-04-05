namespace StorageServer.Client.Commands;

using Amazon.S3;
using Amazon.S3.Model;

using Smart.CommandLine.Hosting;

[Command("object", "Object operations")]
public sealed class ObjectCommand
{
}

[Command("put", "Upload a file as an object")]
public sealed class PutObjectCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public PutObjectCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    [Option<string>("--key", "-k", Description = "Object key", Required = true)]
    public string Key { get; set; } = default!;

    [Option<string>("--file", "-f", Description = "Path to the file to upload", Required = true)]
    public string FilePath { get; set; } = default!;

    [Option<string>("--content-type", "-t", Description = "Content type (auto-detected from file extension if not specified)")]
    public string? ContentType { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        if (!File.Exists(FilePath))
        {
            ConsoleHelper.WriteError($"File not found: {FilePath}");
            context.ExitCode = 1;
            return;
        }

        var contentType = ContentType ?? ResolveContentType(FilePath);

        await using var fileStream = File.OpenRead(FilePath);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Bucket,
            Key = Key,
            InputStream = fileStream,
            ContentType = contentType
        });
        Console.WriteLine($"Uploaded: {FilePath} -> {Bucket}/{Key} ({contentType})");
    }

#pragma warning disable CA1308
    private static string ResolveContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".html" or ".htm" => "text/html",
            ".xml" => "application/xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
#pragma warning restore CA1308
}

[Command("get", "Download an object to a file")]
public sealed class GetObjectCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public GetObjectCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    [Option<string>("--key", "-k", Description = "Object key", Required = true)]
    public string Key { get; set; } = default!;

    [Option<string>("--output", "-o", Description = "Output file path (defaults to the filename part of the key)")]
    public string? OutputPath { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var outputPath = OutputPath ?? Path.GetFileName(Key);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var response = await client.GetObjectAsync(Bucket, Key);
        await using var fileStream = File.Create(outputPath);
        await response.ResponseStream.CopyToAsync(fileStream);

        Console.WriteLine($"Downloaded: {Bucket}/{Key} -> {outputPath}");
        Console.WriteLine($"  ContentType: {response.Headers.ContentType}");
        Console.WriteLine($"  Size: {response.Headers.ContentLength} bytes");
    }
}

[Command("delete", "Delete an object")]
public sealed class DeleteObjectCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public DeleteObjectCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    [Option<string>("--key", "-k", Description = "Object key", Required = true)]
    public string Key { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        await client.DeleteObjectAsync(Bucket, Key);
        Console.WriteLine($"Deleted: {Key} from {Bucket}");
    }
}

[Command("list", "List objects in a bucket")]
public sealed class ListObjectsCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public ListObjectsCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    [Option<string>("--prefix", "-p", Description = "Key prefix filter")]
    public string? Prefix { get; set; }

    [Option<string>("--delimiter", "-d", Description = "Delimiter for hierarchy browsing")]
    public string? Delimiter { get; set; }

    [Option<int>("--max-keys", "-m", Description = "Maximum number of keys to return", DefaultValue = 1000)]
    public int MaxKeys { get; set; }

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var request = new ListObjectsV2Request
        {
            BucketName = Bucket,
            MaxKeys = MaxKeys
        };
        if (Prefix is not null)
        {
            request.Prefix = Prefix;
        }
        if (Delimiter is not null)
        {
            request.Delimiter = Delimiter;
        }

        var response = await client.ListObjectsV2Async(request);

        if (response.CommonPrefixes is { Count: > 0 })
        {
            foreach (var cp in response.CommonPrefixes)
            {
                Console.WriteLine($"  [DIR]  {cp}");
            }
        }

        foreach (var obj in response.S3Objects)
        {
            Console.WriteLine($"  {obj.Key} ({obj.Size} bytes)");
        }

        Console.WriteLine($"Total: {response.S3Objects.Count} objects");
    }
}

[Command("copy", "Copy an object")]
public sealed class CopyObjectCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public CopyObjectCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--source-bucket", "-sb", Description = "Source bucket name", Required = true)]
    public string SourceBucket { get; set; } = default!;

    [Option<string>("--source-key", "-sk", Description = "Source object key", Required = true)]
    public string SourceKey { get; set; } = default!;

    [Option<string>("--bucket", "-b", Description = "Destination bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    [Option<string>("--key", "-k", Description = "Destination object key", Required = true)]
    public string Key { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        await client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = SourceBucket,
            SourceKey = SourceKey,
            DestinationBucket = Bucket,
            DestinationKey = Key
        });
        Console.WriteLine($"Copied: {SourceBucket}/{SourceKey} -> {Bucket}/{Key}");
    }
}

[Command("head", "Get object metadata")]
public sealed class HeadObjectCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public HeadObjectCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    [Option<string>("--key", "-k", Description = "Object key", Required = true)]
    public string Key { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var metadata = await client.GetObjectMetadataAsync(Bucket, Key);
        Console.WriteLine($"Key: {Key}");
        Console.WriteLine($"  ContentType: {metadata.Headers.ContentType}");
        Console.WriteLine($"  ContentLength: {metadata.Headers.ContentLength}");
        Console.WriteLine($"  ETag: {metadata.ETag}");
        Console.WriteLine($"  LastModified: {metadata.LastModified}");

        if (metadata.Metadata.Count > 0)
        {
            Console.WriteLine("  Metadata:");
            foreach (var name in metadata.Metadata.Keys)
            {
                Console.WriteLine($"    {name}: {metadata.Metadata[name]}");
            }
        }
    }
}

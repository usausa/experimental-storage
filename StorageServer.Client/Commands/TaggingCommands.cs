namespace StorageServer.Client.Commands;

using Amazon.S3;
using Amazon.S3.Model;

using Smart.CommandLine.Hosting;

[Command("tag", "Tagging operations")]
public sealed class TagCommand
{
}

[Command("get-object", "Get object tags")]
public sealed class GetObjectTagsCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public GetObjectTagsCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    [Option<string>("--key", "-k", Description = "Object key", Required = true)]
    public string Key { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var response = await client.GetObjectTaggingAsync(
            new GetObjectTaggingRequest { BucketName = Bucket, Key = Key });

        Console.WriteLine($"Tags for {Bucket}/{Key}:");
        foreach (var tag in response.Tagging)
        {
            Console.WriteLine($"  {tag.Key} = {tag.Value}");
        }
    }
}

[Command("put-object", "Set object tags")]
public sealed class PutObjectTagsCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public PutObjectTagsCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    [Option<string>("--key", "-k", Description = "Object key", Required = true)]
    public string Key { get; set; } = default!;

    [Option<string>("--tags", "-t", Description = "Tags as key=value pairs separated by comma (e.g. env=dev,team=backend)", Required = true)]
    public string Tags { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var tagSet = Tags.Split(',')
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => new Tag { Key = parts[0].Trim(), Value = parts[1].Trim() })
            .ToList();

        await client.PutObjectTaggingAsync(new PutObjectTaggingRequest
        {
            BucketName = Bucket,
            Key = Key,
            Tagging = new Tagging { TagSet = tagSet }
        });
        Console.WriteLine($"Tags set on {Bucket}/{Key}");
    }
}

[Command("delete-object", "Delete object tags")]
public sealed class DeleteObjectTagsCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public DeleteObjectTagsCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    [Option<string>("--key", "-k", Description = "Object key", Required = true)]
    public string Key { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        await client.DeleteObjectTaggingAsync(
            new DeleteObjectTaggingRequest { BucketName = Bucket, Key = Key });
        Console.WriteLine($"Tags deleted from {Bucket}/{Key}");
    }
}

[Command("get-bucket", "Get bucket tags")]
public sealed class GetBucketTagsCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public GetBucketTagsCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var response = await client.GetBucketTaggingAsync(
            new GetBucketTaggingRequest { BucketName = Bucket });

        Console.WriteLine($"Tags for bucket {Bucket}:");
        foreach (var tag in response.TagSet)
        {
            Console.WriteLine($"  {tag.Key} = {tag.Value}");
        }
    }
}

[Command("put-bucket", "Set bucket tags")]
public sealed class PutBucketTagsCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public PutBucketTagsCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    [Option<string>("--tags", "-t", Description = "Tags as key=value pairs separated by comma (e.g. project=demo,env=dev)", Required = true)]
    public string Tags { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var tagSet = Tags.Split(',')
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => new Tag { Key = parts[0].Trim(), Value = parts[1].Trim() })
            .ToList();

        await client.PutBucketTaggingAsync(new PutBucketTaggingRequest
        {
            BucketName = Bucket,
            TagSet = tagSet
        });
        Console.WriteLine($"Tags set on bucket {Bucket}");
    }
}

[Command("delete-bucket", "Delete bucket tags")]
public sealed class DeleteBucketTagsCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public DeleteBucketTagsCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--bucket", "-b", Description = "Bucket name", Required = true)]
    public string Bucket { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        await client.DeleteBucketTaggingAsync(Bucket);
        Console.WriteLine($"Tags deleted from bucket {Bucket}");
    }
}

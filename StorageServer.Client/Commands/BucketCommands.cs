namespace StorageServer.Client.Commands;

using Amazon.S3;

using Smart.CommandLine.Hosting;

[Command("bucket", "Bucket operations")]
public sealed class BucketCommand
{
}

[Command("create", "Create a bucket")]
public sealed class CreateBucketCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public CreateBucketCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--name", "-n", Description = "Bucket name", Required = true)]
    public string Name { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        await client.PutBucketAsync(Name);
        Console.WriteLine($"Bucket created: {Name}");
    }
}

[Command("delete", "Delete a bucket")]
public sealed class DeleteBucketCommand : ICommandHandler
{
    private readonly IAmazonS3 client;

    public DeleteBucketCommand(IAmazonS3 client)
    {
        this.client = client;
    }

    [Option<string>("--name", "-n", Description = "Bucket name", Required = true)]
    public string Name { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        await client.DeleteBucketAsync(Name);
        Console.WriteLine($"Bucket deleted: {Name}");
    }
}

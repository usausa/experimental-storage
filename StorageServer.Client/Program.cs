using Amazon.Runtime;
using Amazon.S3;

using Microsoft.Extensions.DependencyInjection;

using Smart.CommandLine.Hosting;

using StorageServer.Client.Commands;

var builder = CommandHost.CreateBuilder(args);

// S3 client
builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var config = new AmazonS3Config
    {
        ServiceURL = "http://localhost:5280",
        ForcePathStyle = true
    };
    return new AmazonS3Client(new BasicAWSCredentials("test", "test"), config);
});

// Commands
builder.ConfigureCommands(commands =>
{
    commands.ConfigureRootCommand(root =>
    {
        root.WithDescription("S3 Storage Client");
    });

    commands.AddCommand<TestCommand>();

    commands.AddCommand<BucketCommand>(bucket =>
    {
        bucket.AddSubCommand<CreateBucketCommand>();
        bucket.AddSubCommand<DeleteBucketCommand>();
    });

    commands.AddCommand<ObjectCommand>(obj =>
    {
        obj.AddSubCommand<PutObjectCommand>();
        obj.AddSubCommand<GetObjectCommand>();
        obj.AddSubCommand<DeleteObjectCommand>();
        obj.AddSubCommand<ListObjectsCommand>();
        obj.AddSubCommand<CopyObjectCommand>();
        obj.AddSubCommand<HeadObjectCommand>();
    });

    commands.AddCommand<TagCommand>(tag =>
    {
        tag.AddSubCommand<GetObjectTagsCommand>();
        tag.AddSubCommand<PutObjectTagsCommand>();
        tag.AddSubCommand<DeleteObjectTagsCommand>();
        tag.AddSubCommand<GetBucketTagsCommand>();
        tag.AddSubCommand<PutBucketTagsCommand>();
        tag.AddSubCommand<DeleteBucketTagsCommand>();
    });
});

var host = builder.Build();
return await host.RunAsync();

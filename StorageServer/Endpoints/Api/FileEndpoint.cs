namespace StorageServer.Endpoints.Api;

using StorageServer.Storage;

public static class FileEndpoint
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapFileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/files");

        group.MapGet("/download/{bucket}/{**key}", HandleDownload);
        group.MapGet("/preview/{bucket}/{**key}", HandlePreview);
        group.MapGet("/thumbnail/{bucket}/{**key}", HandleThumbnail);
        group.MapPost("/upload/{bucket}/{**prefix}", HandleUploadWithPrefix).DisableAntiforgery();
        group.MapPost("/upload/{bucket}", HandleUpload).DisableAntiforgery();
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleDownload(
        HttpContext context,
        string bucket,
        string key,
        string? versionId,
        IStorageService storage)
    {
        ObjectData data;
        if (versionId is not null)
        {
            data = await storage.GetObjectVersionAsync(bucket, key, versionId);
        }
        else
        {
            data = await storage.GetObjectAsync(bucket, key);
        }

        var fileName = Path.GetFileName(key);
        context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
        return Results.Stream(data.Content, data.Head.ContentType, enableRangeProcessing: false);
    }

    private static async ValueTask<IResult> HandlePreview(
        string bucket,
        string key,
        string? versionId,
        IStorageService storage)
    {
        var data = versionId is not null
            ? await storage.GetObjectVersionAsync(bucket, key, versionId)
            : await storage.GetObjectAsync(bucket, key);
        return Results.Stream(data.Content, data.Head.ContentType, enableRangeProcessing: true);
    }

    private static async ValueTask<IResult> HandleThumbnail(
        string bucket,
        string key,
        IStorageService storage)
    {
        var stream = await storage.GetThumbnailAsync(bucket, key);
        if (stream is null)
        {
            return Results.NoContent();
        }
        return Results.Stream(stream, "image/png");
    }

    private static ValueTask<IResult> HandleUploadWithPrefix(
        HttpContext context,
        string bucket,
        string prefix,
        IStorageService storage)
        => UploadFiles(bucket, prefix, context, storage);

    private static ValueTask<IResult> HandleUpload(
        HttpContext context,
        string bucket,
        string? prefix,
        IStorageService storage)
        => UploadFiles(bucket, prefix ?? string.Empty, context, storage);

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> UploadFiles(
        string bucket,
        string prefix,
        HttpContext context,
        IStorageService storage)
    {
        if (!String.IsNullOrEmpty(prefix) && !prefix.EndsWith('/'))
        {
            prefix += "/";
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);

        var uploaded = new List<object>();
        foreach (var file in form.Files)
        {
            var key = prefix + file.FileName;
            await using var stream = file.OpenReadStream();
            var options = new PutObjectOptions { ContentType = file.ContentType };
            var result = await storage.PutObjectAsync(bucket, key, stream, options);
            uploaded.Add(new { key, etag = result.ETag });
        }

        return Results.Ok(new { uploaded });
    }
}

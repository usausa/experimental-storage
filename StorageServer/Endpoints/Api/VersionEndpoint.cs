namespace StorageServer.Endpoints.Api;

using StorageServer.Storage;

public static class VersionEndpoint
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapVersionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/versions");

        group.MapGet("/{bucket}/{**key}", HandleListOrGet);
        group.MapPost("/{bucket}/{**key}", HandleRestore);
        group.MapDelete("/{bucket}/{**key}", HandleDelete);
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleListOrGet(
        HttpContext context,
        string bucket,
        string key,
        string? versionId,
        IStorageService storage)
    {
        if (versionId is not null)
        {
            var data = await storage.GetObjectVersionAsync(bucket, key, versionId);
            var fileName = Path.GetFileName(key);
            context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
            return Results.Stream(data.Content, data.Head.ContentType);
        }

        var versions = await storage.ListVersionsAsync(bucket, key);
        return Results.Ok(new { key, versions });
    }

    private static async ValueTask<IResult> HandleRestore(
        string bucket,
        string key,
        string? versionId,
        IStorageService storage)
    {
        var actualKey = key;
        if (actualKey.EndsWith("/restore", StringComparison.Ordinal))
        {
            actualKey = actualKey[..^"/restore".Length];
        }

        if (versionId is null)
        {
            return Results.BadRequest(new { error = "versionId query parameter is required" });
        }

        await storage.RestoreVersionAsync(bucket, actualKey, versionId);
        return Results.Ok(new { restored = true, versionId });
    }

    private static async ValueTask<IResult> HandleDelete(
        string bucket,
        string key,
        string? versionId,
        string? purge,
        IStorageService storage)
    {
        if (purge is not null)
        {
            await storage.PurgeObjectAsync(bucket, key);
            return Results.Ok(new { purged = true });
        }

        if (versionId is null)
        {
            return Results.BadRequest(new { error = "versionId or purge query parameter is required" });
        }

        await storage.DeleteVersionAsync(bucket, key, versionId);
        return Results.Ok(new { deleted = true, versionId });
    }
}

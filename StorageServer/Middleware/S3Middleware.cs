namespace StorageServer.Middleware;

using System.Globalization;

using StorageServer.Helpers;
using StorageServer.Storage;

/// <summary>
/// Unified S3 middleware that handles request-id, CORS, and exception mapping
/// for all requests under /storage.
/// </summary>
public sealed class S3Middleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IStorageService storageService)
    {
        if (!context.Request.Path.StartsWithSegments("/storage", StringComparison.Ordinal))
        {
            await next(context);
            return;
        }

        // Assign a unique request ID
        var requestId = Guid.NewGuid().ToString("N");
        context.Response.Headers["x-amz-request-id"] = requestId;

        // Apply CORS headers before processing
        await ApplyCorsAsync(context, storageService);
        if (context.Response.HasStarted)
        {
            return;
        }

        // Execute the pipeline and catch storage exceptions
        try
        {
            await next(context);
        }
        catch (StorageException ex) when (!context.Response.HasStarted)
        {
            var result = S3Helper.ToS3Error(ex, requestId);
            await result.ExecuteAsync(context);
        }
    }

    private static async Task ApplyCorsAsync(HttpContext context, IStorageService storageService)
    {
        var origin = context.Request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrEmpty(origin))
        {
            return;
        }

        var pathSegments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments is not { Length: >= 2 })
        {
            return;
        }

        var bucket = pathSegments[1];

        try
        {
            var corsRules = await storageService.GetBucketCorsAsync(bucket);
            var method = context.Request.Method;

            foreach (var rule in corsRules)
            {
                var originMatch = rule.AllowedOrigins.Any(o =>
                    o == "*" || string.Equals(o, origin, StringComparison.OrdinalIgnoreCase));
                if (!originMatch)
                {
                    continue;
                }

                var methodMatch = rule.AllowedMethods.Any(m =>
                    m == "*" || string.Equals(m, method, StringComparison.OrdinalIgnoreCase));
                if (!methodMatch && !HttpMethods.IsOptions(context.Request.Method))
                {
                    continue;
                }

                context.Response.Headers["Access-Control-Allow-Origin"] = origin;

                if (rule.AllowedMethods.Count > 0)
                {
                    context.Response.Headers["Access-Control-Allow-Methods"] = string.Join(", ", rule.AllowedMethods);
                }

                if (rule.AllowedHeaders.Count > 0)
                {
                    context.Response.Headers["Access-Control-Allow-Headers"] = string.Join(", ", rule.AllowedHeaders);
                }

                if (rule.ExposeHeaders.Count > 0)
                {
                    context.Response.Headers["Access-Control-Expose-Headers"] = string.Join(", ", rule.ExposeHeaders);
                }

                if (rule.MaxAgeSeconds > 0)
                {
                    context.Response.Headers["Access-Control-Max-Age"] = rule.MaxAgeSeconds.ToString(CultureInfo.InvariantCulture);
                }

                if (HttpMethods.IsOptions(context.Request.Method))
                {
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.CompleteAsync();
                }

                break;
            }
        }
        catch (StorageException ex) when (string.Equals(ex.ErrorCode, "NoSuchCORSConfiguration", StringComparison.Ordinal))
        {
            // No CORS config — proceed without CORS headers
        }
    }
}

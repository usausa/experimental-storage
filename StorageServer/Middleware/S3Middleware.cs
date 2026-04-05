namespace StorageServer.Middleware;

using System.Globalization;
using System.Xml.Linq;
using StorageServer.Consts;

using StorageServer.Storage;

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
            var result = ToS3Error(ex, requestId);
            await result.ExecuteAsync(context);
        }
    }

    private static async Task ApplyCorsAsync(HttpContext context, IStorageService storageService)
    {
        var origin = context.Request.Headers.Origin.FirstOrDefault();
        if (String.IsNullOrEmpty(origin))
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
                var originMatch = rule.AllowedOrigins.Any(x => x == "*" || String.Equals(x, origin, StringComparison.OrdinalIgnoreCase));
                if (!originMatch)
                {
                    continue;
                }

                var methodMatch = rule.AllowedMethods.Any(x => x == "*" || String.Equals(x, method, StringComparison.OrdinalIgnoreCase));
                if (!methodMatch && !HttpMethods.IsOptions(context.Request.Method))
                {
                    continue;
                }

                context.Response.Headers["Access-Control-Allow-Origin"] = origin;

                if (rule.AllowedMethods.Count > 0)
                {
                    context.Response.Headers["Access-Control-Allow-Methods"] = String.Join(", ", rule.AllowedMethods);
                }

                if (rule.AllowedHeaders.Count > 0)
                {
                    context.Response.Headers["Access-Control-Allow-Headers"] = String.Join(", ", rule.AllowedHeaders);
                }

                if (rule.ExposeHeaders.Count > 0)
                {
                    context.Response.Headers["Access-Control-Expose-Headers"] = String.Join(", ", rule.ExposeHeaders);
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
        catch (StorageException ex) when (String.Equals(ex.ErrorCode, "NoSuchCORSConfiguration", StringComparison.Ordinal))
        {
            // No CORS config
        }
    }

    private static IResult ToS3Error(StorageException ex, string requestId)
    {
        if (ex is NotModifiedException)
        {
            return Results.StatusCode(304);
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Names.Error,
                new XElement(S3Names.Code, ex.ErrorCode),
                new XElement(S3Names.Message, ex.Message),
                new XElement(S3Names.RequestId, requestId)));

        return Results.Content(
            doc.Declaration + doc.ToString(),
            "application/xml",
            statusCode: ex.HttpStatusCode);
    }
}

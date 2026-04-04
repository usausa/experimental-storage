namespace StorageServer.Middleware;

/// <summary>
/// Rewrites incoming S3 requests to internal /storage/* paths.
///
/// Supported access patterns:
///   1. Virtual-hosted style: {bucket}.s3.localhost/{key} → /storage/{bucket}/{key}
///   2. Path style on s3.localhost: s3.localhost/{bucket}/{key} → /storage/{bucket}/{key}
///   3. Path style on localhost (ForcePathStyle=true):
///      localhost:5280/{bucket}/{key} → /storage/{bucket}/{key}
/// </summary>
public class VirtualHostStyleMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly string baseHostname = configuration.GetValue<string>("Storage:BaseHostname") ?? "s3.localhost";

    private static readonly string[] AppPrefixes =
    [
        "/storage",
        "/api",
        "/health",
        "/alive",
        "/_framework",
        "/_blazor",
        "/_content",
        "/Components",
        "/lib",
        "/css",
        "/js",
        "/fonts",
        "/favicon"
    ];

    private static readonly string[] BlazorRoutes =
    [
        "/browse",
        "/not-found"
    ];

    public Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        var path = context.Request.Path.Value ?? "/";

        if (host.EndsWith("." + baseHostname, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(host, baseHostname, StringComparison.OrdinalIgnoreCase))
        {
            var bucket = host[..^(baseHostname.Length + 1)];
            context.Request.Path = $"/storage/{bucket}" + context.Request.Path;
        }
        else if (string.Equals(host, baseHostname, StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Path = "/storage" + context.Request.Path;
        }
        else if (!IsAppPath(path) && path.Length > 1)
        {
            context.Request.Path = "/storage" + context.Request.Path;
        }

        return next(context);
    }

    private static bool IsAppPath(string path)
    {
        if (path == "/")
        {
            return true;
        }

        foreach (var prefix in AppPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var route in BlazorRoutes)
        {
            if (path.StartsWith(route, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var firstSegment = path.AsSpan()[1..];
        var slashIndex = firstSegment.IndexOf('/');
        if (slashIndex >= 0)
        {
            firstSegment = firstSegment[..slashIndex];
        }

        if (firstSegment.Contains('.'))
        {
            return true;
        }

        return false;
    }
}

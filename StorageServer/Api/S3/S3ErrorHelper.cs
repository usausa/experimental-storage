namespace StorageServer.Api.S3;

using System.Xml.Linq;

using StorageServer.Storage;

/// <summary>
/// Converts <see cref="StorageException"/> to S3-compatible XML error responses.
/// </summary>
public static class S3ErrorHelper
{
    private static readonly XNamespace S3Ns = "http://s3.amazonaws.com/doc/2006-03-01/";

    public static IResult ToS3Error(StorageException ex, string? requestId = null)
    {
        if (ex is NotModifiedException)
        {
            return Results.StatusCode(304);
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(S3Ns + "Error",
                new XElement(S3Ns + "Code", ex.ErrorCode),
                new XElement(S3Ns + "Message", ex.Message),
                new XElement(S3Ns + "RequestId", requestId ?? Guid.NewGuid().ToString("N"))));

        return Results.Content(
            doc.Declaration + doc.ToString(),
            "application/xml",
            statusCode: ex.HttpStatusCode);
    }
}

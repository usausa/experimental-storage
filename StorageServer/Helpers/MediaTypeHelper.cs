namespace StorageServer.Helpers;

public static class MediaTypeHelper
{
#pragma warning disable CA1308
    public static bool IsImage(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".bmp" or ".ico";
    }
#pragma warning restore CA1308

#pragma warning disable CA1308
    public static bool IsVideo(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext is ".mp4" or ".webm";
    }
#pragma warning restore CA1308

#pragma warning disable CA1308
    public static bool IsAudio(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext is ".mp3" or ".wav" or ".ogg";
    }
#pragma warning restore CA1308

#pragma warning disable CA1308
    public static bool IsPdf(string name)
    {
        return Path.GetExtension(name).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }
#pragma warning restore CA1308

#pragma warning disable CA1308
    public static bool IsText(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext is ".txt" or ".log" or ".md" or ".csv" or ".json" or ".xml" or ".yaml" or ".yml"
            or ".cs" or ".js" or ".ts" or ".py" or ".go" or ".java" or ".html" or ".css" or ".sh" or ".ps1"
            or ".toml" or ".ini" or ".cfg" or ".conf" or ".env";
    }
#pragma warning restore CA1308

#pragma warning disable CA1308
    public static string GetFileIcon(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "bi-file-earmark-pdf text-danger",
            ".doc" or ".docx" => "bi-file-earmark-word text-primary",
            ".xls" or ".xlsx" => "bi-file-earmark-excel text-success",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" => "bi-file-earmark-image text-info",
            ".mp4" or ".webm" => "bi-file-earmark-play text-warning",
            ".mp3" or ".wav" or ".ogg" => "bi-file-earmark-music",
            ".zip" or ".tar" or ".gz" or ".7z" => "bi-file-earmark-zip",
            ".json" or ".xml" or ".yaml" or ".yml" => "bi-file-earmark-code",
            ".cs" or ".js" or ".ts" or ".py" or ".go" or ".java" => "bi-file-earmark-code",
            ".txt" or ".log" or ".md" or ".csv" => "bi-file-earmark-text",
            _ => "bi-file-earmark"
        };
    }
#pragma warning restore CA1308

#pragma warning disable CA1308
    public static string GetContentType(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".mp4" or ".webm" => $"video/{ext[1..]}",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream"
        };
    }
#pragma warning restore CA1308
}

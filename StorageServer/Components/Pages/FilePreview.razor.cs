namespace StorageServer.Components.Pages;

using Microsoft.AspNetCore.Components;

using StorageServer.Storage;

public partial class FilePreview
{
    [Inject]
    public IStorageService Storage { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public string Bucket { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public string Key { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public long Size { get; set; }

    [Parameter]
    [EditorRequired]
    public EventCallback OnClose { get; set; }

    private string? textContent;

    private string FileName => Path.GetFileName(Key);
    private string PreviewUrl => $"/api/files/preview/{Uri.EscapeDataString(Bucket)}/{EncodeKey(Key)}";
    private string DownloadUrl => $"/api/files/download/{Uri.EscapeDataString(Bucket)}/{EncodeKey(Key)}";

    private string ContentType => Helpers.MediaTypeHelper.GetContentType(Key);
    private string FormattedSize => Helpers.FormatHelper.FormatBytes(Size);
    private string IconCss => Helpers.MediaTypeHelper.GetFileIcon(FileName);

    private bool IsImageFile => Helpers.MediaTypeHelper.IsImage(Key);
    private bool IsVideoFile => Helpers.MediaTypeHelper.IsVideo(Key);
    private bool IsAudioFile => Helpers.MediaTypeHelper.IsAudio(Key);
    private bool IsPdfFile => Helpers.MediaTypeHelper.IsPdf(Key);
    private bool IsTextFile => Helpers.MediaTypeHelper.IsText(Key);

    protected override async Task OnParametersSetAsync()
    {
        if (IsTextFile && Size <= 1024 * 1024)
        {
            try
            {
                var data = await Storage.GetObjectAsync(Bucket, Key);
                using var reader = new StreamReader(data.Content);
                textContent = await reader.ReadToEndAsync();
            }
            catch (StorageServer.Storage.StorageException)
            {
                textContent = string.Empty;
            }
        }
    }

    private static string EncodeKey(string key) =>
        String.Join("/", key.Split('/').Select(Uri.EscapeDataString));
}

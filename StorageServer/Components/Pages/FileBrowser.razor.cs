namespace StorageServer.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using StorageServer.Storage;
using StorageServer.Storage.Models;

public partial class FileBrowser : IAsyncDisposable
{
    [Parameter]
    public string Bucket { get; set; } = string.Empty;

    [Parameter]
    public string? Prefix { get; set; }

    private List<string> directories = [];
    private List<ObjectSummary> objects = [];
    private bool loading = true;
    private string? error;
    private string sortBy = "name";
    private bool sortAsc = true;

    private bool creatingFolder;
    private string newFolderName = string.Empty;

    // Upload state
    private bool uploading;
    private int uploadTotalFiles;
    private int uploadCompletedFiles;
    private long uploadTotalBytes;
    private long uploadCompletedBytes;
    private int UploadPercent => uploadTotalBytes > 0 ? (int)(100.0 * uploadCompletedBytes / uploadTotalBytes) : 0;

    // JS interop for drag & drop
    private ElementReference dropZoneRef;
    private ElementReference fileInputRef;
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<FileBrowser>? dotNetRef;

    // Preview & metadata & versions
    private string? previewKey;
    private long previewSize;
    private string? metadataKey;
    private string? versionKey;
    private List<VersionInfo> versions = [];
    private bool versionLoading;

    // Overwrite confirmation
    private bool showOverwriteConfirm;
    private List<string> overwriteFileNames = [];
    private TaskCompletionSource<bool>? overwriteTcs;

    private IEnumerable<ObjectSummary> SortedObjects => sortBy switch
    {
        "size" => sortAsc ? objects.OrderBy(o => o.Size) : objects.OrderByDescending(o => o.Size),
        "modified" => sortAsc ? objects.OrderBy(o => o.LastModified) : objects.OrderByDescending(o => o.LastModified),
        _ => sortAsc ? objects.OrderBy(o => o.Key) : objects.OrderByDescending(o => o.Key)
    };

    protected override Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(Prefix) && !Prefix.EndsWith('/'))
        {
            Prefix += "/";
        }

        return LoadObjects();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            dotNetRef = DotNetObjectReference.Create(this);
            jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/fileUpload.js");
            await jsModule.InvokeVoidAsync("initDropZone", dropZoneRef, fileInputRef, dotNetRef);
        }
    }

    private async Task LoadObjects()
    {
        loading = true;
        error = null;
        try
        {
            var result = await Storage.ListObjectsAsync(Bucket, new ListObjectsOptions
            {
                Prefix = Prefix,
                Delimiter = "/"
            });
            directories = result.CommonPrefixes.ToList();
            objects = result.Objects.Where(o => o.Key != Prefix).ToList();
        }
        catch (StorageException ex)
        {
            error = ex.Message;
        }
        finally
        {
            loading = false;
        }
    }

    private void NavigateTo(string prefix) => Nav.NavigateTo($"/browse/{Bucket}/{prefix}");

    private void NavigateUp()
    {
        if (string.IsNullOrEmpty(Prefix))
        {
            return;
        }

        var trimmed = Prefix.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        var parent = lastSlash >= 0 ? trimmed[..(lastSlash + 1)] : string.Empty;
        Nav.NavigateTo(string.IsNullOrEmpty(parent) ? $"/browse/{Bucket}" : $"/browse/{Bucket}/{parent}");
    }

    private List<(string Name, string Path, bool IsLast)> GetBreadcrumbs()
    {
        if (string.IsNullOrEmpty(Prefix))
        {
            return [];
        }

        var parts = Prefix.TrimEnd('/').Split('/');
        var result = new List<(string, string, bool)>();
        for (var i = 0; i < parts.Length; i++)
        {
            var path = string.Join("/", parts.Take(i + 1)) + "/";
            result.Add((parts[i], path, i == parts.Length - 1));
        }

        return result;
    }

    private void ToggleSort(string column)
    {
        if (sortBy == column)
        {
            sortAsc = !sortAsc;
        }
        else
        {
            sortBy = column;
            sortAsc = true;
        }
    }

    private string SortIcon(string column)
    {
        if (sortBy != column)
        {
            return string.Empty;
        }

        return sortAsc ? "\u25b2" : "\u25bc";
    }

    private void ShowNewFolderInput()
    {
        creatingFolder = true;
        newFolderName = string.Empty;
    }

    private Task HandleFolderKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            return CreateFolder();
        }
        if (e.Key == "Escape")
        {
            creatingFolder = false;
        }
        return Task.CompletedTask;
    }

    private async Task CreateFolder()
    {
        if (string.IsNullOrWhiteSpace(newFolderName))
        {
            return;
        }

        try
        {
            var key = (Prefix ?? string.Empty) + newFolderName.Trim().TrimEnd('/') + "/";
            await using var empty = new MemoryStream([]);
            await Storage.PutObjectAsync(Bucket, key, empty);
            creatingFolder = false;
            await LoadObjects();
        }
        catch (StorageException ex)
        {
            error = ex.Message;
        }
    }

    private async Task DeleteObject(string key)
    {
        try
        {
            await Storage.DeleteObjectAsync(Bucket, key);
            await LoadObjects();
        }
        catch (StorageException ex)
        {
            error = ex.Message;
        }
    }

    private async Task DeletePrefix(string prefix)
    {
        try
        {
            var result = await Storage.ListObjectsAsync(Bucket, new ListObjectsOptions { Prefix = prefix });
            var keys = result.Objects.Select(o => o.Key).ToList();
            if (keys.Count > 0)
            {
                await Storage.DeleteObjectsAsync(Bucket, keys);
            }

            await LoadObjects();
        }
        catch (StorageException ex)
        {
            error = ex.Message;
        }
    }

    // ---- Upload via JS interop ----

    private async Task TriggerFileInput()
    {
        if (jsModule is not null)
        {
            await JS.InvokeVoidAsync("eval", "document.querySelector('.fb input[type=file]').click()");
        }
    }

    [JSInvokable]
    public string GetCurrentBucket() => Bucket;

    [JSInvokable]
    public string GetCurrentPath() => Prefix?.TrimEnd('/') ?? string.Empty;

    [JSInvokable]
    public Task OnUploadStarted(int totalFiles, long totalBytes)
    {
        uploading = true;
        uploadTotalFiles = totalFiles;
        uploadTotalBytes = totalBytes;
        uploadCompletedFiles = 0;
        uploadCompletedBytes = 0;
        StateHasChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnUploadByteProgress(int completedFiles, int totalFiles, long completedBytes, long totalBytes)
    {
        uploadCompletedFiles = completedFiles;
        uploadTotalFiles = totalFiles;
        uploadCompletedBytes = completedBytes;
        uploadTotalBytes = totalBytes;
        StateHasChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnUploadError(string message)
    {
        error = message;
        StateHasChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public async Task OnUploadCompleted()
    {
        uploading = false;
        await LoadObjects();
        StateHasChanged();
    }

    // ---- Preview & Metadata ----

    private void OpenPreview(string key, long size)
    {
        previewKey = key;
        previewSize = size;
    }

    private void ClosePreview() => previewKey = null;

    private void OpenMetadata(string key) => metadataKey = key;
    private void CloseMetadata() => metadataKey = null;

    // ---- Versions ----

    private async Task OpenVersions(string key)
    {
        versionKey = key;
        versionLoading = true;
        versions = [];
        try
        {
            var result = await Storage.ListVersionsAsync(Bucket, key);
            versions = result.ToList();
        }
        catch (StorageException ex)
        {
            error = ex.Message;
        }
        finally
        {
            versionLoading = false;
        }
    }

    private void CloseVersions() => versionKey = null;

    private async Task RestoreVersion(string versionId)
    {
        if (versionKey is null)
        {
            return;
        }

        try
        {
            await Storage.RestoreVersionAsync(Bucket, versionKey, versionId);
            await OpenVersions(versionKey);
            await LoadObjects();
        }
        catch (StorageException ex)
        {
            error = ex.Message;
        }
    }

    private async Task DeleteVersion(string versionId)
    {
        if (versionKey is null)
        {
            return;
        }

        try
        {
            await Storage.DeleteVersionAsync(Bucket, versionKey, versionId);
            await OpenVersions(versionKey);
        }
        catch (StorageException ex)
        {
            error = ex.Message;
        }
    }

    // ---- Overwrite confirmation ----

    [JSInvokable]
    public async Task<bool> CheckDuplicates(string[] fileNames)
    {
        var existingKeys = objects.Select(o =>
        {
            var key = o.Key;
            if (!string.IsNullOrEmpty(Prefix))
            {
                key = key[Prefix.Length..];
            }

            return key;
        }).ToHashSet(StringComparer.Ordinal);

        var duplicates = fileNames.Where(existingKeys.Contains).ToList();
        if (duplicates.Count == 0)
        {
            return true;
        }

        overwriteFileNames = duplicates;
        showOverwriteConfirm = true;
        overwriteTcs = new TaskCompletionSource<bool>();
        StateHasChanged();

        return await overwriteTcs.Task;
    }

    private void ConfirmOverwrite()
    {
        showOverwriteConfirm = false;
        overwriteTcs?.TrySetResult(true);
    }

    private void CancelOverwrite()
    {
        showOverwriteConfirm = false;
        overwriteTcs?.TrySetResult(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (jsModule is not null)
        {
            await jsModule.DisposeAsync();
        }

        dotNetRef?.Dispose();
        GC.SuppressFinalize(this);
    }
}

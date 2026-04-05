namespace StorageServer.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using StorageServer.Storage;

public partial class FileBrowser : IAsyncDisposable
{
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
    private string uploadCurrentFileName = string.Empty;
    private int UploadPercent => uploadTotalBytes > 0 ? (int)(100.0 * uploadCompletedBytes / uploadTotalBytes) : 0;

    // JS interop for drag & drop
    private ElementReference dropZoneRef;
    private ElementReference fileInputRef;
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<FileBrowser>? dotNetRef;

    // Preview & metadata & versions
    private string? previewKey;
    private long previewSize;
    private string? previewVersionId;
    private string? metadataKey;
    private string? versionKey;
    private List<VersionInfo> versions = [];
    private bool versionLoading;

    // Overwrite confirmation
    private bool showOverwriteConfirm;
    private List<string> overwriteFileNames = [];
    private TaskCompletionSource<bool>? overwriteTcs;

    // Delete confirmation
    private enum DeleteChoice
    {
        None,
        Soft,
        Hard
    }
    private bool showDeleteConfirm;
    private string deleteDisplayName = string.Empty;
    private bool deleteIsFolder;
    private bool deleteShowSoftOption = true;
    private TaskCompletionSource<DeleteChoice>? deleteTcs;

    private IEnumerable<ObjectSummary> SortedObjects => sortBy switch
    {
        "size" => sortAsc ? objects.OrderBy(static x => x.Size) : objects.OrderByDescending(static x => x.Size),
        "modified" => sortAsc ? objects.OrderBy(static x => x.LastModified) : objects.OrderByDescending(static x => x.LastModified),
        _ => sortAsc ? objects.OrderBy(static x => x.Key) : objects.OrderByDescending(static x => x.Key)
    };

    //--------------------------------------------------------------------------------
    // Parameter
    //--------------------------------------------------------------------------------

    [Inject]
    public IStorageService Storage { get; set; } = default!;

    [Inject]
    public NavigationManager Nav { get; set; } = default!;

    [Inject]
    public IJSRuntime JS { get; set; } = default!;

    [Parameter]
    public string Bucket { get; set; } = string.Empty;

    [Parameter]
    public string? Prefix { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
        if (jsModule is not null)
        {
            try
            {
                await jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected; JS interop is no longer available
            }
            catch (ObjectDisposedException)
            {
                // Already disposed
            }
        }

        dotNetRef?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override Task OnParametersSetAsync()
    {
        if (!String.IsNullOrEmpty(Prefix) && !Prefix.EndsWith('/'))
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

    //--------------------------------------------------------------------------------
    // Load
    //--------------------------------------------------------------------------------

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
            objects = result.Objects.Where(x => x.Key != Prefix).ToList();

            // Include soft-deleted objects at the current prefix level
            var currentPrefix = Prefix ?? string.Empty;
            var deleted = await Storage.ListDeletedObjectsAsync(Bucket, currentPrefix.Length > 0 ? currentPrefix : null);
            var directDeleted = deleted.Where(x =>
            {
                var afterPrefix = x.Key[currentPrefix.Length..];
                return !afterPrefix.Contains('/', StringComparison.Ordinal);
            });
            objects = objects.Concat(directDeleted).ToList();
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

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    private void NavigateTo(string prefix) => Nav.NavigateTo($"/browse/{Bucket}/{prefix}");

    private void NavigateUp()
    {
        if (String.IsNullOrEmpty(Prefix))
        {
            return;
        }

        var trimmed = Prefix.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        var parent = lastSlash >= 0 ? trimmed[..(lastSlash + 1)] : string.Empty;
        Nav.NavigateTo(String.IsNullOrEmpty(parent) ? $"/browse/{Bucket}" : $"/browse/{Bucket}/{parent}");
    }

    //--------------------------------------------------------------------------------
    // Data
    //--------------------------------------------------------------------------------

    private List<(string Name, string Path, bool IsLast)> GetBreadcrumbs()
    {
        if (String.IsNullOrEmpty(Prefix))
        {
            return [];
        }

        var parts = Prefix.TrimEnd('/').Split('/');
        var result = new List<(string, string, bool)>();
        for (var i = 0; i < parts.Length; i++)
        {
            var path = String.Join("/", parts.Take(i + 1)) + "/";
            result.Add((parts[i], path, i == parts.Length - 1));
        }

        return result;
    }

    //--------------------------------------------------------------------------------
    // Action
    //--------------------------------------------------------------------------------

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
        if (String.IsNullOrWhiteSpace(newFolderName))
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
        var displayName = String.IsNullOrEmpty(Prefix) ? key : key[Prefix.Length..];
        var choice = await ConfirmDeleteAsync(displayName, isFolder: false);
        if (choice == DeleteChoice.None)
        {
            return;
        }

        try
        {
            if (choice == DeleteChoice.Hard)
            {
                await Storage.PurgeObjectAsync(Bucket, key);
            }
            else
            {
                await Storage.DeleteObjectAsync(Bucket, key);
            }
            await LoadObjects();
        }
        catch (StorageException ex)
        {
            error = ex.Message;
        }
    }

    private async Task DeletePrefix(string prefix)
    {
        var dirName = prefix.TrimEnd('/').Split('/').Last();
        var choice = await ConfirmDeleteAsync(dirName, isFolder: true);
        if (choice == DeleteChoice.None)
        {
            return;
        }

        try
        {
            var result = await Storage.ListObjectsAsync(Bucket, new ListObjectsOptions { Prefix = prefix });
            var keys = result.Objects.Select(static x => x.Key).ToList();
            if (keys.Count > 0)
            {
                if (choice == DeleteChoice.Hard)
                {
                    foreach (var key in keys)
                    {
                        await Storage.PurgeObjectAsync(Bucket, key);
                    }
                }
                else
                {
                    await Storage.DeleteObjectsAsync(Bucket, keys);
                }
            }

            await LoadObjects();
        }
        catch (StorageException ex)
        {
            error = ex.Message;
        }
    }

    //--------------------------------------------------------------------------------
    // Upload
    //--------------------------------------------------------------------------------

    private async Task TriggerFileInput()
    {
        if (jsModule is not null)
        {
            await JS.InvokeVoidAsync("eval", "document.querySelector('.fb input[type=file]').click()");
        }
    }

    [JSInvokable]
#pragma warning disable CA1024
    public string GetCurrentBucket() => Bucket;
#pragma warning restore CA1024

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
        uploadCurrentFileName = string.Empty;
        StateHasChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnUploadByteProgress(int completedFiles, int totalFiles, long completedBytes, long totalBytes, string currentFileName)
    {
        uploadCompletedFiles = completedFiles;
        uploadTotalFiles = totalFiles;
        uploadCompletedBytes = completedBytes;
        uploadTotalBytes = totalBytes;
        uploadCurrentFileName = currentFileName;
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

    //--------------------------------------------------------------------------------
    // Preview
    //--------------------------------------------------------------------------------

    private void OpenPreview(string key, long size)
    {
        previewKey = key;
        previewSize = size;
    }

    private void OpenPreview(string key, long size, string? versionId)
    {
        previewKey = key;
        previewSize = size;
        previewVersionId = versionId;
    }

    private void ClosePreview()
    {
        previewKey = null;
        previewVersionId = null;
    }

    private void OpenMetadata(string key) => metadataKey = key;
    private void CloseMetadata() => metadataKey = null;

    //--------------------------------------------------------------------------------
    // Deleted object
    //--------------------------------------------------------------------------------

    private async Task PurgeDeletedObject(string key)
    {
        var displayName = String.IsNullOrEmpty(Prefix) ? key : key[Prefix.Length..];
        var choice = await ConfirmDeleteAsync(displayName, isFolder: false, showSoftOption: false);
        if (choice != DeleteChoice.Hard)
        {
            return;
        }

        try
        {
            await Storage.PurgeObjectAsync(Bucket, key);
            await LoadObjects();
        }
        catch (StorageException ex)
        {
            error = ex.Message;
        }
    }

    //--------------------------------------------------------------------------------
    // Versions
    //--------------------------------------------------------------------------------

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

    //--------------------------------------------------------------------------------
    // Overwrite confirmation
    //--------------------------------------------------------------------------------

    [JSInvokable]
    public async Task<bool> CheckDuplicates(string[] fileNames)
    {
        var existingKeys = objects.Select(x =>
        {
            var key = x.Key;
            if (!String.IsNullOrEmpty(Prefix))
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

    private Task<DeleteChoice> ConfirmDeleteAsync(string displayName, bool isFolder, bool showSoftOption = true)
    {
        deleteDisplayName = displayName;
        deleteIsFolder = isFolder;
        deleteShowSoftOption = showSoftOption;
        showDeleteConfirm = true;
        deleteTcs = new TaskCompletionSource<DeleteChoice>();
        StateHasChanged();
        return deleteTcs.Task;
    }

    private void ConfirmSoftDeleteAction()
    {
        showDeleteConfirm = false;
        deleteTcs?.TrySetResult(DeleteChoice.Soft);
    }

    private void ConfirmHardDeleteAction()
    {
        showDeleteConfirm = false;
        deleteTcs?.TrySetResult(DeleteChoice.Hard);
    }

    private void CancelDeleteAction()
    {
        showDeleteConfirm = false;
        deleteTcs?.TrySetResult(DeleteChoice.None);
    }
}

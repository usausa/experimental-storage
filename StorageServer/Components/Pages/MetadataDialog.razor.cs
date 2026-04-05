namespace StorageServer.Components.Pages;

using Microsoft.AspNetCore.Components;

using StorageServer.Storage;
using StorageServer.Storage.Models;

public partial class MetadataDialog
{
    private sealed record KvPair(string Key, string Value);

    private ObjectMetadata? metadata;
    private List<KvPair> editUserMeta = [];
    private List<KvPair> editTags = [];
    private bool loading = true;
    private bool saving;
    private string? saveError;

    private string FileName => Path.GetFileName(Key);

    //--------------------------------------------------------------------------------
    // Parameter
    //--------------------------------------------------------------------------------

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
    public EventCallback OnClose { get; set; }

    [Parameter]
    public EventCallback OnSaved { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override async Task OnParametersSetAsync()
    {
        loading = true;
        saveError = null;
        try
        {
            metadata = await Storage.GetObjectMetadataAsync(Bucket, Key);
            editUserMeta = metadata.UserMetadata.Select(static x => new KvPair(x.Key, x.Value)).ToList();
            editTags = metadata.Tags.Select(static x => new KvPair(x.Key, x.Value)).ToList();
        }
        catch (StorageException ex)
        {
            saveError = ex.Message;
        }
        finally
        {
            loading = false;
        }
    }

    //--------------------------------------------------------------------------------
    // Action
    //--------------------------------------------------------------------------------

    private void AddUserMeta() => editUserMeta.Add(new KvPair(string.Empty, string.Empty));

    private void AddTag() => editTags.Add(new KvPair(string.Empty, string.Empty));

    private void UpdateUserMetaKey(int index, ChangeEventArgs e) =>
        editUserMeta[index] = editUserMeta[index] with { Key = e.Value?.ToString() ?? string.Empty };

    private void UpdateUserMetaValue(int index, ChangeEventArgs e) =>
        editUserMeta[index] = editUserMeta[index] with { Value = e.Value?.ToString() ?? string.Empty };

    private void UpdateTagKey(int index, ChangeEventArgs e) =>
        editTags[index] = editTags[index] with { Key = e.Value?.ToString() ?? string.Empty };

    private void UpdateTagValue(int index, ChangeEventArgs e) =>
        editTags[index] = editTags[index] with { Value = e.Value?.ToString() ?? string.Empty };

    private async Task SaveChanges()
    {
        saving = true;
        saveError = null;
        try
        {
            var newUserMeta = editUserMeta
                .Where(static x => !String.IsNullOrWhiteSpace(x.Key))
                .ToDictionary(static x => x.Key.Trim(), static x => x.Value);
            var newTags = editTags
                .Where(static x => !String.IsNullOrWhiteSpace(x.Key))
                .ToDictionary(static x => x.Key.Trim(), static x => x.Value);

            await Storage.UpdateObjectMetadataAsync(Bucket, Key, new ObjectMetadataPatch
            {
                UserMetadata = newUserMeta,
                Tags = newTags
            });

            await OnSaved.InvokeAsync();
            await OnClose.InvokeAsync();
        }
        catch (StorageException ex)
        {
            saveError = ex.Message;
        }
        finally
        {
            saving = false;
        }
    }
}

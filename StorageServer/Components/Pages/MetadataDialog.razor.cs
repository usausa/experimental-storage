namespace StorageServer.Components.Pages;

using Microsoft.AspNetCore.Components;

using StorageServer.Storage;
using StorageServer.Storage.Models;

public partial class MetadataDialog
{
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

    private sealed record KvPair(string Key, string Value);

    private ObjectMetadata? metadata;
    private List<KvPair> editUserMeta = [];
    private List<KvPair> editTags = [];
    private bool loading = true;
    private bool saving;
    private string? saveError;

    private string FileName => Path.GetFileName(Key);

    protected override async Task OnParametersSetAsync()
    {
        loading = true;
        saveError = null;
        try
        {
            metadata = await Storage.GetObjectMetadataAsync(Bucket, Key);
            editUserMeta = metadata.UserMetadata
                .Select(kv => new KvPair(kv.Key, kv.Value)).ToList();
            editTags = metadata.Tags
                .Select(kv => new KvPair(kv.Key, kv.Value)).ToList();
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

    private void AddUserMeta() => editUserMeta.Add(new KvPair(string.Empty, string.Empty));
    private void AddTag() => editTags.Add(new KvPair(string.Empty, string.Empty));

    private void UpdateUserMetaKey(int idx, ChangeEventArgs e) =>
        editUserMeta[idx] = editUserMeta[idx] with { Key = e.Value?.ToString() ?? string.Empty };

    private void UpdateUserMetaValue(int idx, ChangeEventArgs e) =>
        editUserMeta[idx] = editUserMeta[idx] with { Value = e.Value?.ToString() ?? string.Empty };

    private void UpdateTagKey(int idx, ChangeEventArgs e) =>
        editTags[idx] = editTags[idx] with { Key = e.Value?.ToString() ?? string.Empty };

    private void UpdateTagValue(int idx, ChangeEventArgs e) =>
        editTags[idx] = editTags[idx] with { Value = e.Value?.ToString() ?? string.Empty };

    private async Task SaveChanges()
    {
        saving = true;
        saveError = null;
        try
        {
            var newUserMeta = editUserMeta
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .ToDictionary(kv => kv.Key.Trim(), kv => kv.Value);
            var newTags = editTags
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .ToDictionary(kv => kv.Key.Trim(), kv => kv.Value);

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

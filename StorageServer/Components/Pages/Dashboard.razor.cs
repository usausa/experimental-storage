namespace StorageServer.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using StorageServer.Storage;
using StorageServer.Storage.Models;

public partial class Dashboard
{
    private sealed record BucketRow(BucketInfo Info, BucketStats? Stats);

    private List<BucketRow> buckets = [];
    private bool loading = true;
    private string? error;
    private bool showCreate;
    private string newBucketName = string.Empty;
    private string? createError;
    private bool showDeleteConfirm;
    private string deleteBucketName = string.Empty;

    // Bucket tags
    private string? tagsBucketName;
    private bool tagsLoading;
    private bool tagsSaving;
    private string? tagsError;
    private List<KeyValuePair<string, string>> editBucketTags = [];

    protected override Task OnInitializedAsync()
    {
        return LoadBuckets();
    }

    private async Task LoadBuckets()
    {
        loading = true;
        error = null;
        try
        {
            var infos = await Storage.ListBucketsAsync();
            var rows = new List<BucketRow>();
            foreach (var info in infos)
            {
                BucketStats? stats = null;
                try
                {
                    stats = await Storage.GetBucketStatsAsync(info.Name);
                }
                catch (StorageException)
                {
                    // Stats may be unavailable for newly created buckets
                }

                rows.Add(new BucketRow(info, stats));
            }

            buckets = rows;
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

    private void ShowCreateDialog()
    {
        newBucketName = string.Empty;
        createError = null;
        showCreate = true;
    }

    private Task HandleCreateKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            return CreateBucket();
        }
        if (e.Key == "Escape")
        {
            showCreate = false;
        }
        return Task.CompletedTask;
    }

    private async Task CreateBucket()
    {
        createError = null;
        if (string.IsNullOrWhiteSpace(newBucketName))
        {
            createError = "Bucket name is required.";
            return;
        }
        try
        {
            await Storage.CreateBucketAsync(newBucketName.Trim());
            showCreate = false;
            await LoadBuckets();
        }
        catch (StorageException ex)
        {
            createError = ex.Message;
        }
    }

    private void ConfirmDeleteBucket(string name)
    {
        deleteBucketName = name;
        showDeleteConfirm = true;
    }

    private async Task DeleteBucket()
    {
        try
        {
            await Storage.DeleteBucketAsync(deleteBucketName, force: true);
            showDeleteConfirm = false;
            await LoadBuckets();
        }
        catch (StorageException ex)
        {
            error = ex.Message;
            showDeleteConfirm = false;
        }
    }

    // ---- Bucket Tags ----

    private async Task OpenBucketTags(string name)
    {
        tagsBucketName = name;
        tagsLoading = true;
        tagsError = null;
        editBucketTags = [];
        try
        {
            var tags = await Storage.GetBucketTagsAsync(name);
            editBucketTags = tags.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)).ToList();
        }
        catch (StorageException ex)
        {
            tagsError = ex.Message;
        }
        finally
        {
            tagsLoading = false;
        }
    }

    private void CloseBucketTags() => tagsBucketName = null;

    private void AddBucketTag()
    {
        editBucketTags.Add(new KeyValuePair<string, string>(string.Empty, string.Empty));
    }

    private void UpdateBucketTagKey(int index, ChangeEventArgs e)
    {
        var newKey = e.Value?.ToString() ?? string.Empty;
        editBucketTags[index] = new KeyValuePair<string, string>(newKey, editBucketTags[index].Value);
    }

    private void UpdateBucketTagValue(int index, ChangeEventArgs e)
    {
        var newValue = e.Value?.ToString() ?? string.Empty;
        editBucketTags[index] = new KeyValuePair<string, string>(editBucketTags[index].Key, newValue);
    }

    private async Task SaveBucketTags()
    {
        if (tagsBucketName is null)
        {
            return;
        }

        tagsSaving = true;
        tagsError = null;
        try
        {
            var tags = editBucketTags
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            await Storage.PutBucketTagsAsync(tagsBucketName, tags);
            tagsBucketName = null;
        }
        catch (StorageException ex)
        {
            tagsError = ex.Message;
        }
        finally
        {
            tagsSaving = false;
        }
    }
}

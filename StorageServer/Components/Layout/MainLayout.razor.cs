namespace StorageServer.Components.Layout;

using StorageServer.Storage;

public partial class MainLayout
{
    private int bucketCount;
    private long totalObjects;
    private long totalSize;

    protected override async Task OnInitializedAsync()
    {
        var buckets = await Storage.ListBucketsAsync();
        bucketCount = buckets.Count;
        foreach (var bucket in buckets)
        {
            try
            {
                var stats = await Storage.GetBucketStatsAsync(bucket.Name);
                totalObjects += stats.ObjectCount;
                totalSize += stats.TotalSizeBytes;
            }
            catch (StorageException)
            {
                // Bucket stats may fail for newly created empty buckets
            }
        }
    }
}

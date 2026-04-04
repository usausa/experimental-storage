namespace StorageServer.Storage.Models;

public sealed record ObjectData : IAsyncDisposable
{
    public required ObjectHead Head { get; init; }
    public required Stream Content { get; init; }

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

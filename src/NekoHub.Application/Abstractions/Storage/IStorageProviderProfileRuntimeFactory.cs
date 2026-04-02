using NekoHub.Domain.Storage;

namespace NekoHub.Application.Abstractions.Storage;

public interface IStorageProviderProfileRuntimeFactory
{
    AssetStorageLease CreateStorageLease(StorageProviderProfile profile);

    StorageProviderRuntimeDescriptor Describe(StorageProviderProfile profile);
}

public sealed record StorageProviderRuntimeDescriptor(
    string ProviderType,
    string ProviderName,
    StorageProviderCapabilities Capabilities);

public sealed class AssetStorageLease : IAsyncDisposable
{
    private readonly IAsyncDisposable? _asyncDisposable;
    private readonly IDisposable? _disposable;

    private AssetStorageLease(IAssetStorage storage, bool ownsStorage)
    {
        Storage = storage;

        if (!ownsStorage)
        {
            return;
        }

        _asyncDisposable = storage as IAsyncDisposable;
        _disposable = storage as IDisposable;
    }

    public IAssetStorage Storage { get; }

    public static AssetStorageLease Shared(IAssetStorage storage)
    {
        return new AssetStorageLease(storage, ownsStorage: false);
    }

    public static AssetStorageLease Owned(IAssetStorage storage)
    {
        return new AssetStorageLease(storage, ownsStorage: true);
    }

    public ValueTask DisposeAsync()
    {
        if (_asyncDisposable is not null)
        {
            return _asyncDisposable.DisposeAsync();
        }

        _disposable?.Dispose();
        return ValueTask.CompletedTask;
    }
}

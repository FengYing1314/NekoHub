using NekoHub.Application.Abstractions.Storage;

namespace NekoHub.Application.Assets.Services;

public interface IAssetStorageTargetSelector
{
    Task<AssetStorageTargetSelectionResult> ResolveWriteTargetAsync(
        Guid? requestedProfileId,
        CancellationToken cancellationToken = default);

    Task<AssetStorageLease> ResolveReadTargetAsync(
        Guid? boundProfileId,
        string legacyStorageProvider,
        CancellationToken cancellationToken = default);
}

public sealed class AssetStorageTargetSelectionResult : IAsyncDisposable
{
    private readonly AssetStorageLease _storageLease;

    public AssetStorageTargetSelectionResult(
        AssetStorageLease storageLease,
        Guid? storageProviderProfileId,
        string selectionSource)
    {
        _storageLease = storageLease;
        StorageProviderProfileId = storageProviderProfileId;
        SelectionSource = selectionSource;
    }

    public IAssetStorage Storage => _storageLease.Storage;

    public Guid? StorageProviderProfileId { get; }

    public string SelectionSource { get; }

    public ValueTask DisposeAsync()
    {
        return _storageLease.DisposeAsync();
    }
}

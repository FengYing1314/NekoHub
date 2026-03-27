using NekoHub.Application.Abstractions.Storage;

namespace NekoHub.Application.Assets.Services;

public interface IAssetStorageTargetSelector
{
    Task<AssetStorageTargetSelectionResult> ResolveWriteTargetAsync(
        Guid? requestedProfileId,
        CancellationToken cancellationToken = default);

    Task<IAssetStorage> ResolveReadTargetAsync(
        Guid? boundProfileId,
        string legacyStorageProvider,
        CancellationToken cancellationToken = default);
}

public sealed record AssetStorageTargetSelectionResult(
    IAssetStorage Storage,
    Guid? StorageProviderProfileId,
    string SelectionSource);

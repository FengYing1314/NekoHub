using NekoHub.Domain.Storage;

namespace NekoHub.Application.Abstractions.Storage;

public interface IAssetStorage
{
    string ProviderName { get; }

    string ProviderType { get; }

    StorageProviderCapabilities Capabilities { get; }

    bool SupportsWrite { get; }

    Task<StoredAssetObject> StoreAsync(
        Stream content,
        StoreAssetRequest request,
        CancellationToken cancellationToken = default);

    Task<StoredAssetObject> OverwriteAsync(
        Stream content,
        string storageKey,
        StoreAssetRequest request,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(DeleteStoredAssetRequest request, CancellationToken cancellationToken = default);

    Task<string?> GetPublicUrlAsync(string storageKey, CancellationToken cancellationToken = default);
}

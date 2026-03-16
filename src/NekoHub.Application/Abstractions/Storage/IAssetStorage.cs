namespace NekoHub.Application.Abstractions.Storage;

public interface IAssetStorage
{
    string ProviderName { get; }

    Task<StoredAssetObject> StoreAsync(
        Stream content,
        StoreAssetRequest request,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<string?> GetPublicUrlAsync(string storageKey, CancellationToken cancellationToken = default);
}

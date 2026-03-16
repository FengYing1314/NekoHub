namespace NekoHub.Application.Abstractions.Storage;

public interface IAssetStorageResolver
{
    IAssetStorage Resolve(StorageProvider provider);

    IAssetStorage Resolve(string providerName);

    IAssetStorage ResolveByProviderType(string providerType);

    IAssetStorage ResolveDefault();
}

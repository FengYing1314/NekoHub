using Microsoft.Extensions.Options;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Infrastructure.Options;

namespace NekoHub.Infrastructure.Storage;

public sealed class AssetStorageResolver : IAssetStorageResolver
{
    private readonly StorageOptions _storageOptions;
    private readonly IReadOnlyDictionary<string, IAssetStorage> _storagesByProviderName;
    private readonly IReadOnlyDictionary<string, IAssetStorage> _storagesByProviderType;

    public AssetStorageResolver(
        IEnumerable<IAssetStorage> storages,
        IOptions<StorageOptions> storageOptions)
    {
        _storageOptions = storageOptions.Value;
        _storagesByProviderName = CreateStorageMap(storages, static storage => storage.ProviderName, "provider name");
        _storagesByProviderType = CreateStorageMap(storages, static storage => storage.ProviderType, "provider type");
    }

    public IAssetStorage Resolve(StorageProvider provider)
    {
        return Resolve(provider.ToProviderName());
    }

    public IAssetStorage Resolve(string providerName)
    {
        var normalizedProviderName = NormalizeProviderName(providerName, "provider name");
        if (!_storagesByProviderName.TryGetValue(normalizedProviderName, out var storage))
        {
            throw new InvalidOperationException(
                $"Unsupported storage provider '{normalizedProviderName}'. Currently supported providers: {GetSupportedProvidersDisplay()}.");
        }

        return storage;
    }

    public IAssetStorage ResolveByProviderType(string providerType)
    {
        var normalizedProviderType = NormalizeProviderName(providerType, "provider type");
        if (!_storagesByProviderType.TryGetValue(normalizedProviderType, out var storage))
        {
            throw new InvalidOperationException(
                $"Unsupported storage provider type '{normalizedProviderType}'. Currently supported provider types: {GetSupportedProviderTypesDisplay()}.");
        }

        return storage;
    }

    public IAssetStorage ResolveDefault()
    {
        var configuredProviderName = NormalizeProviderName(_storageOptions.Provider, "Storage:Provider");
        if (!_storagesByProviderName.TryGetValue(configuredProviderName, out var storage))
        {
            throw new InvalidOperationException(
                $"Unsupported default storage provider '{configuredProviderName}'. Currently supported providers: {GetSupportedProvidersDisplay()}.");
        }

        return storage;
    }

    private static IReadOnlyDictionary<string, IAssetStorage> CreateStorageMap(
        IEnumerable<IAssetStorage> storages,
        Func<IAssetStorage, string> keySelector,
        string keyDisplayName)
    {
        var map = new Dictionary<string, IAssetStorage>(StringComparer.OrdinalIgnoreCase);

        foreach (var storage in storages)
        {
            var key = NormalizeProviderName(keySelector(storage), $"IAssetStorage.{keyDisplayName}");
            if (!map.TryAdd(key, storage))
            {
                var existing = map[key];
                throw new InvalidOperationException(
                    $"Duplicate storage {keyDisplayName} '{key}' detected: '{existing.GetType().Name}' and '{storage.GetType().Name}'.");
            }
        }

        if (map.Count == 0)
        {
            throw new InvalidOperationException("No asset storage providers are registered.");
        }

        return map;
    }

    private string GetSupportedProvidersDisplay()
    {
        return string.Join(", ", _storagesByProviderName.Keys.OrderBy(static provider => provider, StringComparer.OrdinalIgnoreCase));
    }

    private string GetSupportedProviderTypesDisplay()
    {
        return string.Join(", ", _storagesByProviderType.Keys.OrderBy(static provider => provider, StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizeProviderName(string? providerName, string name)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        return providerName.Trim();
    }
}

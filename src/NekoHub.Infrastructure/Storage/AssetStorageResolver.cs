using Microsoft.Extensions.Options;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Infrastructure.Options;

namespace NekoHub.Infrastructure.Storage;

public sealed class AssetStorageResolver : IAssetStorageResolver
{
    private readonly StorageOptions _storageOptions;
    private readonly IReadOnlyDictionary<string, IAssetStorage> _storages;

    public AssetStorageResolver(
        IEnumerable<IAssetStorage> storages,
        IOptions<StorageOptions> storageOptions)
    {
        _storageOptions = storageOptions.Value;
        _storages = CreateStorageMap(storages);
    }

    public IAssetStorage Resolve(StorageProvider provider)
    {
        return Resolve(provider.ToProviderName());
    }

    public IAssetStorage Resolve(string providerName)
    {
        var normalizedProviderName = NormalizeProviderName(providerName, "provider name");
        if (!_storages.TryGetValue(normalizedProviderName, out var storage))
        {
            throw new InvalidOperationException(
                $"Unsupported storage provider '{normalizedProviderName}'. Currently supported providers: {GetSupportedProvidersDisplay()}.");
        }

        return storage;
    }

    public IAssetStorage ResolveDefault()
    {
        var configuredProviderName = NormalizeProviderName(_storageOptions.Provider, "Storage:Provider");
        if (!_storages.TryGetValue(configuredProviderName, out var storage))
        {
            throw new InvalidOperationException(
                $"Unsupported default storage provider '{configuredProviderName}'. Currently supported providers: {GetSupportedProvidersDisplay()}.");
        }

        return storage;
    }

    private static IReadOnlyDictionary<string, IAssetStorage> CreateStorageMap(IEnumerable<IAssetStorage> storages)
    {
        var map = new Dictionary<string, IAssetStorage>(StringComparer.OrdinalIgnoreCase);

        foreach (var storage in storages)
        {
            var providerName = NormalizeProviderName(storage.ProviderName, "IAssetStorage.ProviderName");
            if (!map.TryAdd(providerName, storage))
            {
                var existing = map[providerName];
                throw new InvalidOperationException(
                    $"Duplicate storage provider '{providerName}' detected: '{existing.GetType().Name}' and '{storage.GetType().Name}'.");
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
        return string.Join(", ", _storages.Keys.OrderBy(static provider => provider, StringComparer.OrdinalIgnoreCase));
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

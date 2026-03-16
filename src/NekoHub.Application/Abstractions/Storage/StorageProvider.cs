namespace NekoHub.Application.Abstractions.Storage;

public enum StorageProvider
{
    Local
}

public static class StorageProviderExtensions
{
    public const string LocalProviderName = "local";

    public static string ToProviderName(this StorageProvider provider)
    {
        return provider switch
        {
            StorageProvider.Local => LocalProviderName,
            _ => throw new InvalidOperationException($"Unsupported storage provider '{provider}'.")
        };
    }

    public static bool TryParseProvider(string? providerName, out StorageProvider provider)
    {
        if (string.Equals(providerName, LocalProviderName, StringComparison.OrdinalIgnoreCase))
        {
            provider = StorageProvider.Local;
            return true;
        }

        provider = default;
        return false;
    }
}

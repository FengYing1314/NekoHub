namespace NekoHub.Application.Abstractions.Storage;

public enum StorageProvider
{
    Local,
    S3Compatible,
    GitHubRepo
}

public static class StorageProviderExtensions
{
    public const string LocalProviderName = "local";
    public const string S3CompatibleProviderName = "s3";
    public const string GitHubRepoProviderName = "github-repo";

    public static string ToProviderName(this StorageProvider provider)
    {
        return provider switch
        {
            StorageProvider.Local => LocalProviderName,
            StorageProvider.S3Compatible => S3CompatibleProviderName,
            StorageProvider.GitHubRepo => GitHubRepoProviderName,
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

        if (string.Equals(providerName, S3CompatibleProviderName, StringComparison.OrdinalIgnoreCase))
        {
            provider = StorageProvider.S3Compatible;
            return true;
        }

        if (string.Equals(providerName, GitHubRepoProviderName, StringComparison.OrdinalIgnoreCase))
        {
            provider = StorageProvider.GitHubRepo;
            return true;
        }

        provider = default;
        return false;
    }
}

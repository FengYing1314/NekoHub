using NekoHub.Application.Abstractions.Storage;
using NekoHub.Infrastructure.Options;

namespace NekoHub.Infrastructure.Storage.Local;

public sealed class LocalAssetStorage(
    Microsoft.Extensions.Options.IOptions<LocalStorageOptions> localStorageOptions,
    Microsoft.Extensions.Options.IOptions<StorageOptions> storageOptions) : IAssetStorage
{
    private readonly LocalStorageOptions _localStorageOptions = localStorageOptions.Value;
    private readonly StorageOptions _storageOptions = storageOptions.Value;
    public string ProviderName => StorageProviderExtensions.LocalProviderName;

    public async Task<StoredAssetObject> StoreAsync(
        Stream content,
        StoreAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        var rootPath = Path.GetFullPath(_localStorageOptions.RootPath);

        if (_localStorageOptions.CreateDirectoryIfMissing)
        {
            Directory.CreateDirectory(rootPath);
        }

        var safeExtension = NormalizeExtension(request.Extension);
        var storageKey = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.CreateVersion7():N}{safeExtension}";
        var physicalPath = ResolvePhysicalPath(rootPath, storageKey);

        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        await using (var target = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        var publicUrl = BuildPublicUrl(storageKey);
        return new StoredAssetObject(
            ProviderName,
            storageKey,
            publicUrl,
            Path.GetFileName(physicalPath));
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var rootPath = Path.GetFullPath(_localStorageOptions.RootPath);
        var physicalPath = ResolvePhysicalPath(rootPath, storageKey);

        if (!File.Exists(physicalPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var rootPath = Path.GetFullPath(_localStorageOptions.RootPath);
        var physicalPath = ResolvePhysicalPath(rootPath, storageKey);
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetPublicUrlAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BuildPublicUrl(storageKey));
    }

    private string? BuildPublicUrl(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(_storageOptions.PublicBaseUrl))
        {
            return null;
        }

        var baseUri = _storageOptions.PublicBaseUrl.TrimEnd('/');
        var normalizedKey = storageKey.Replace('\\', '/').TrimStart('/');
        var encodedSegments = normalizedKey
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);

        // 按路径段编码，保留分隔符，确保 URL 在 /content/... 路由下可正确访问。
        return $"{baseUri}/{string.Join('/', encodedSegments)}";
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.StartsWith('.') ? extension : $".{extension}";
        return normalized.ToLowerInvariant();
    }

    private static string ResolvePhysicalPath(string rootPath, string storageKey)
    {
        var normalizedKey = storageKey.Replace('\\', '/').TrimStart('/');
        var path = Path.GetFullPath(Path.Combine(rootPath, normalizedKey));

        // Use relative path validation instead of prefix matching so sibling paths
        // like "<root>2/..." cannot bypass the root directory boundary check.
        var relativePath = Path.GetRelativePath(rootPath, path);
        var escapesRoot = relativePath == ".."
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath);

        if (escapesRoot)
        {
            throw new InvalidOperationException("Invalid storage key path.");
        }

        return path;
    }
}

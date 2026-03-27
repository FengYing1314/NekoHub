using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Infrastructure.Storage.GitHub;

internal sealed class GitHubRepoPathResolver
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _reference;
    private readonly string _apiBaseUrl;
    private readonly string _rawBaseUrl;

    public GitHubRepoPathResolver(
        string owner,
        string repo,
        string reference,
        string apiBaseUrl,
        string rawBaseUrl,
        string? basePath)
    {
        _owner = owner.Trim();
        _repo = repo.Trim();
        _reference = reference.Trim();
        _apiBaseUrl = apiBaseUrl.TrimEnd('/');
        _rawBaseUrl = rawBaseUrl.TrimEnd('/');
        BasePath = NormalizePath(basePath, allowEmpty: true, "storage_provider_path_invalid", "BasePath contains invalid segments.");
    }

    public string BasePath { get; }

    public string NormalizeRelativePath(string? relativePath, bool allowEmpty = false)
    {
        return NormalizePath(
            relativePath,
            allowEmpty,
            "storage_provider_relative_path_invalid",
            "Relative path contains invalid segments.");
    }

    public string NormalizeStorageKey(string? storageKey)
    {
        return NormalizePath(
            storageKey,
            allowEmpty: false,
            "storage_provider_storage_key_invalid",
            "Storage key contains invalid segments.");
    }

    public string BuildStorageKey(string relativePath)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        return Combine(BasePath, normalizedRelativePath);
    }

    public string BuildStoragePrefix(string? relativePath)
    {
        var normalizedRelativePath = NormalizeRelativePath(relativePath, allowEmpty: true);
        return Combine(BasePath, normalizedRelativePath);
    }

    public string ToRelativePath(string storageKey)
    {
        var normalizedStorageKey = NormalizeStorageKey(storageKey);
        if (string.IsNullOrWhiteSpace(BasePath))
        {
            return normalizedStorageKey;
        }

        if (string.Equals(normalizedStorageKey, BasePath, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var prefix = $"{BasePath}/";
        if (normalizedStorageKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedStorageKey[prefix.Length..];
        }

        return normalizedStorageKey;
    }

    public Uri BuildContentsApiUri(string? repoPath)
    {
        var normalizedRepoPath = NormalizePath(
            repoPath,
            allowEmpty: true,
            "storage_provider_storage_key_invalid",
            "Repository path contains invalid segments.");

        var encodedOwner = Uri.EscapeDataString(_owner);
        var encodedRepo = Uri.EscapeDataString(_repo);
        var encodedRef = Uri.EscapeDataString(_reference);

        var pathPart = string.IsNullOrWhiteSpace(normalizedRepoPath)
            ? string.Empty
            : $"/{EncodePath(normalizedRepoPath)}";

        return new Uri($"{_apiBaseUrl}/repos/{encodedOwner}/{encodedRepo}/contents{pathPart}?ref={encodedRef}");
    }

    public Uri BuildRawUri(string storageKey)
    {
        var normalizedStorageKey = NormalizeStorageKey(storageKey);
        var encodedOwner = Uri.EscapeDataString(_owner);
        var encodedRepo = Uri.EscapeDataString(_repo);
        var encodedRef = Uri.EscapeDataString(_reference);
        var encodedPath = EncodePath(normalizedStorageKey);
        return new Uri($"{_rawBaseUrl}/{encodedOwner}/{encodedRepo}/{encodedRef}/{encodedPath}");
    }

    private static string Combine(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        return $"{first}/{second}";
    }

    private static string NormalizePath(
        string? path,
        bool allowEmpty,
        string code,
        string message)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (allowEmpty)
            {
                return string.Empty;
            }

            throw new ValidationException(code, message);
        }

        var normalized = path.Trim().Replace('\\', '/').Trim('/');
        if (normalized.Length == 0)
        {
            if (allowEmpty)
            {
                return string.Empty;
            }

            throw new ValidationException(code, message);
        }

        if (normalized.Contains("//", StringComparison.Ordinal))
        {
            throw new ValidationException(code, message);
        }

        var segments = normalized.Split('/');
        if (segments.Any(static segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new ValidationException(code, message);
        }

        return string.Join('/', segments);
    }

    private static string EncodePath(string path)
    {
        return string.Join(
            '/',
            path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }
}

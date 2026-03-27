using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Storage;
using NekoHub.Infrastructure.Options;

namespace NekoHub.Infrastructure.Storage.GitHub;

public sealed class GitHubRepoAssetStorage : IAssetStorage, IGitHubRepoStorage
{
    public const string HttpClientName = "github-repo-storage";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly GitHubRepoStorageOptions _options;
    private readonly Func<HttpClient> _httpClientFactory;
    private readonly Lazy<GitHubRepoPathResolver> _pathResolver;

    public GitHubRepoAssetStorage(
        Microsoft.Extensions.Options.IOptions<GitHubRepoStorageOptions> options,
        IHttpClientFactory httpClientFactory)
        : this(options, () => httpClientFactory.CreateClient(HttpClientName))
    {
    }

    internal GitHubRepoAssetStorage(
        Microsoft.Extensions.Options.IOptions<GitHubRepoStorageOptions> options,
        Func<HttpClient> httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _pathResolver = new Lazy<GitHubRepoPathResolver>(CreatePathResolver, isThreadSafe: true);
    }

    public string ProviderName => string.IsNullOrWhiteSpace(_options.ProviderName)
        ? GitHubRepoStorageOptions.DefaultProviderName
        : _options.ProviderName.Trim();

    public string ProviderType => StorageProviderTypes.GitHubRepo;

    public StorageProviderCapabilities Capabilities => StorageProviderCapabilityCatalog.GetRequired(ProviderType);

    public bool SupportsWrite => true;

    public async Task<StoredAssetObject> StoreAsync(
        Stream content,
        StoreAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        var relativePath = BuildGeneratedRelativePath(request.Extension);
        var upsert = await UpsertFileAsync(
            content,
            new GitHubRepoUpsertFileRequest(
                RelativePath: relativePath,
                CommitMessage: BuildCommitMessage(
                    relativePath,
                    Path.GetFileName(relativePath),
                    operation: "upload")),
            cancellationToken);

        return new StoredAssetObject(
            Provider: ProviderName,
            StorageKey: upsert.StorageKey,
            PublicUrl: upsert.PublicUrl,
            StoredFileName: Path.GetFileName(upsert.StorageKey));
    }

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var normalizedStorageKey = PathResolver.NormalizeStorageKey(storageKey);

        if (ShouldUseRawReadPath())
        {
            var stream = await DownloadRawAsync(normalizedStorageKey, cancellationToken);
            if (stream is not null)
            {
                return stream;
            }
        }

        return await DownloadViaContentsApiAsync(normalizedStorageKey, cancellationToken);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        throw new ValidationException(
            "storage_provider_delete_not_supported",
            "github-repo provider does not support delete.");
    }

    public Task<string?> GetPublicUrlAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var normalizedStorageKey = PathResolver.NormalizeStorageKey(storageKey);
        if (IsPrivateTokenVisibilityPolicy())
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(PathResolver.BuildRawUri(normalizedStorageKey).ToString());
    }

    public async Task<IReadOnlyList<GitHubRepoDirectoryEntry>> ListDirectoryAsync(
        string? relativePath = null,
        bool recursive = false,
        int maxDepth = 2,
        CancellationToken cancellationToken = default)
    {
        if (maxDepth < 1)
        {
            throw new ValidationException(
                "storage_provider_directory_depth_invalid",
                "maxDepth must be greater than or equal to 1.");
        }

        var rootPrefix = PathResolver.BuildStoragePrefix(relativePath);
        var results = new List<GitHubRepoDirectoryEntry>();

        await ListDirectoryInternalAsync(rootPrefix, recursive, maxDepth, currentDepth: 1, results, cancellationToken);
        return results;
    }

    public async Task<GitHubRepoUpsertFileResult> UpsertFileAsync(
        Stream content,
        GitHubRepoUpsertFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var relativePath = PathResolver.NormalizeRelativePath(request.RelativePath);
        var storageKey = PathResolver.BuildStorageKey(relativePath);
        var payloadBytes = await ReadAllBytesAsync(content, cancellationToken);

        var existingFileMetadata = await TryGetFileMetadataAsync(storageKey, cancellationToken);
        var expectedSha = NormalizeExpectedSha(request.ExpectedSha);
        EnsureExpectedShaMatches(storageKey, expectedSha, existingFileMetadata);

        var requestBody = new GitHubPutContentRequest(
            Message: BuildCommitMessage(
                relativePath,
                Path.GetFileName(relativePath),
                operation: existingFileMetadata is null ? "create" : "update",
                overrideMessage: request.CommitMessage),
            Content: Convert.ToBase64String(payloadBytes),
            Branch: _options.Ref?.Trim() ?? "main",
            Sha: existingFileMetadata?.Sha);

        var requestMessage = CreateRequest(HttpMethod.Put, PathResolver.BuildContentsApiUri(storageKey), includeToken: true);
        requestMessage.Content = JsonContent.Create(requestBody, options: SerializerOptions);

        using var response = await SendAsync(requestMessage, cancellationToken);
        if (response.StatusCode is not HttpStatusCode.Created and not HttpStatusCode.OK)
        {
            await ThrowForFailedResponseAsync(
                response,
                "storage_provider_upsert_failed",
                $"Failed to upsert '{relativePath}' in github-repo provider.",
                cancellationToken);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var contentNode = document.RootElement.TryGetProperty("content", out var node) ? node : default;
        var resolvedStorageKey = TryGetString(contentNode, "path") ?? storageKey;
        var sha = TryGetString(contentNode, "sha") ?? existingFileMetadata?.Sha ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sha))
        {
            throw new ValidationException(
                "storage_provider_upsert_result_invalid",
                "github-repo upsert response did not return file sha.");
        }

        var finalRelativePath = PathResolver.ToRelativePath(resolvedStorageKey);
        var publicUrl = IsPrivateTokenVisibilityPolicy()
            ? null
            : PathResolver.BuildRawUri(resolvedStorageKey).ToString();

        return new GitHubRepoUpsertFileResult(
            StorageKey: resolvedStorageKey,
            RelativePath: finalRelativePath,
            Sha: sha,
            Created: response.StatusCode == HttpStatusCode.Created,
            PublicUrl: publicUrl);
    }

    private static void EnsureExpectedShaMatches(
        string storageKey,
        string? expectedSha,
        GitHubFileMetadata? existingFileMetadata)
    {
        if (string.IsNullOrWhiteSpace(expectedSha))
        {
            return;
        }

        if (existingFileMetadata is null)
        {
            throw new ConflictException(
                "storage_provider_upsert_expected_sha_target_missing",
                $"Target '{storageKey}' does not exist, expectedSha cannot be matched.");
        }

        if (string.Equals(existingFileMetadata.Sha, expectedSha, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new ConflictException(
            "storage_provider_upsert_expected_sha_conflict",
            $"expectedSha '{expectedSha}' does not match current sha '{existingFileMetadata.Sha}'.");
    }

    private async Task ListDirectoryInternalAsync(
        string storagePrefix,
        bool recursive,
        int maxDepth,
        int currentDepth,
        List<GitHubRepoDirectoryEntry> results,
        CancellationToken cancellationToken)
    {
        var entries = await GetDirectoryEntriesAsync(storagePrefix, cancellationToken);
        foreach (var entry in entries)
        {
            var entryStoragePath = TryGetString(entry, "path");
            var entryName = TryGetString(entry, "name");
            var type = TryGetString(entry, "type");
            if (string.IsNullOrWhiteSpace(entryStoragePath) || string.IsNullOrWhiteSpace(entryName) || string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            var isDirectory = string.Equals(type, "dir", StringComparison.OrdinalIgnoreCase);
            var relativePath = PathResolver.ToRelativePath(entryStoragePath);
            var publicUrl = !isDirectory && !IsPrivateTokenVisibilityPolicy()
                ? PathResolver.BuildRawUri(entryStoragePath).ToString()
                : null;

            results.Add(new GitHubRepoDirectoryEntry(
                Name: entryName,
                RelativePath: relativePath,
                IsDirectory: isDirectory,
                Size: TryGetInt64(entry, "size"),
                Sha: TryGetString(entry, "sha"),
                PublicUrl: publicUrl));

            if (!recursive || !isDirectory || currentDepth >= maxDepth)
            {
                continue;
            }

            await ListDirectoryInternalAsync(
                storagePrefix: entryStoragePath,
                recursive: true,
                maxDepth: maxDepth,
                currentDepth: currentDepth + 1,
                results: results,
                cancellationToken: cancellationToken);
        }
    }

    private async Task<IReadOnlyList<JsonElement>> GetDirectoryEntriesAsync(string storagePath, CancellationToken cancellationToken)
    {
        var request = CreateRequest(HttpMethod.Get, PathResolver.BuildContentsApiUri(storagePath), includeToken: true);
        using var response = await SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException(
                "storage_provider_directory_not_found",
                $"Directory '{PathResolver.ToRelativePath(storagePath)}' was not found in github-repo provider.");
        }

        if (!response.IsSuccessStatusCode)
        {
            await ThrowForFailedResponseAsync(
                response,
                "storage_provider_directory_list_failed",
                "Failed to list github-repo directory.",
                cancellationToken);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            var type = TryGetString(document.RootElement, "type");
            if (string.Equals(type, "dir", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            throw new ValidationException(
                "storage_provider_directory_expected",
                "Directory listing target points to a file path.");
        }

        if (document.RootElement.ValueKind is not JsonValueKind.Array)
        {
            throw new ValidationException(
                "storage_provider_directory_list_failed",
                "Unexpected github-repo directory response payload.");
        }

        return document.RootElement
            .EnumerateArray()
            .Select(static entry => entry.Clone())
            .ToList();
    }

    private async Task<GitHubFileMetadata?> TryGetFileMetadataAsync(string storageKey, CancellationToken cancellationToken)
    {
        var request = CreateRequest(HttpMethod.Get, PathResolver.BuildContentsApiUri(storageKey), includeToken: true);
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            await ThrowForFailedResponseAsync(
                response,
                "storage_provider_read_failed",
                "Failed to resolve github-repo file metadata.",
                cancellationToken);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var type = TryGetString(document.RootElement, "type");
        if (!string.Equals(type, "file", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "storage_provider_file_expected",
                "Target path is not a file.");
        }

        return new GitHubFileMetadata(
            Path: TryGetString(document.RootElement, "path") ?? storageKey,
            Sha: TryGetString(document.RootElement, "sha") ?? string.Empty,
            DownloadUrl: TryGetString(document.RootElement, "download_url"),
            Encoding: TryGetString(document.RootElement, "encoding"),
            Base64Content: TryGetString(document.RootElement, "content"));
    }

    private async Task<Stream?> DownloadViaContentsApiAsync(string storageKey, CancellationToken cancellationToken)
    {
        var metadata = await TryGetFileMetadataAsync(storageKey, cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        if (string.Equals(metadata.Encoding, "base64", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(metadata.Base64Content))
        {
            var normalizedBase64 = metadata.Base64Content.Replace("\n", string.Empty).Replace("\r", string.Empty);
            var bytes = Convert.FromBase64String(normalizedBase64);
            return new MemoryStream(bytes, writable: false);
        }

        if (!string.IsNullOrWhiteSpace(metadata.DownloadUrl))
        {
            var request = CreateRequest(HttpMethod.Get, new Uri(metadata.DownloadUrl), includeToken: true);
            using var response = await SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                await ThrowForFailedResponseAsync(
                    response,
                    "storage_provider_read_failed",
                    $"Failed to download github-repo file '{storageKey}'.",
                    cancellationToken);
            }

            return await ReadContentAsMemoryStreamAsync(response, cancellationToken);
        }

        throw new ValidationException(
            "storage_provider_read_failed",
            $"github-repo file '{storageKey}' does not expose readable content.");
    }

    private async Task<Stream?> DownloadRawAsync(string storageKey, CancellationToken cancellationToken)
    {
        var rawUri = PathResolver.BuildRawUri(storageKey);
        var request = CreateRequest(HttpMethod.Get, rawUri, includeToken: false);
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            await ThrowForFailedResponseAsync(
                response,
                "storage_provider_read_failed",
                $"Failed to download github-repo raw content '{storageKey}'.",
                cancellationToken);
        }

        return await ReadContentAsMemoryStreamAsync(response, cancellationToken);
    }

    private static async Task<MemoryStream> ReadContentAsMemoryStreamAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return memory;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory().SendAsync(request, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, bool includeToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("NekoHub", "1.0"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        if (includeToken && !string.IsNullOrWhiteSpace(_options.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token.Trim());
        }

        return request;
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream content, CancellationToken cancellationToken)
    {
        if (content is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var buffer))
        {
            return buffer.ToArray();
        }

        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        return copy.ToArray();
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static long? TryGetInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt64(out var value) => value,
            _ => null
        };
    }

    private async Task ThrowForFailedResponseAsync(
        HttpResponseMessage response,
        string code,
        string fallbackMessage,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ValidationException(
                "storage_provider_access_denied",
                "github-repo access was denied. Check token and repository visibility settings.");
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(detail))
        {
            throw new ValidationException(code, $"{fallbackMessage} Response: {detail}");
        }

        throw new ValidationException(code, fallbackMessage);
    }

    private string BuildGeneratedRelativePath(string extension)
    {
        var normalizedExtension = NormalizeExtension(extension);
        return $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.CreateVersion7():N}{normalizedExtension}";
    }

    private string BuildCommitMessage(
        string relativePath,
        string fileName,
        string operation,
        string? overrideMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideMessage))
        {
            return overrideMessage.Trim();
        }

        var template = string.IsNullOrWhiteSpace(_options.CommitMessageTemplate)
            ? "chore(nekohub): {operation} {path}"
            : _options.CommitMessageTemplate.Trim();

        return template
            .Replace("{path}", relativePath, StringComparison.OrdinalIgnoreCase)
            .Replace("{fileName}", fileName, StringComparison.OrdinalIgnoreCase)
            .Replace("{operation}", operation, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldUseRawReadPath()
    {
        return !IsPrivateTokenVisibilityPolicy() && string.IsNullOrWhiteSpace(_options.Token);
    }

    private bool IsPrivateTokenVisibilityPolicy()
    {
        return string.Equals(_options.VisibilityPolicy?.Trim(), "private-token", StringComparison.OrdinalIgnoreCase);
    }

    private GitHubRepoPathResolver PathResolver => _pathResolver.Value;

    private GitHubRepoPathResolver CreatePathResolver()
    {
        if (string.IsNullOrWhiteSpace(_options.Owner))
        {
            throw new InvalidOperationException("Storage:GitHubRepo:Owner is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.Repo))
        {
            throw new InvalidOperationException("Storage:GitHubRepo:Repo is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.Ref))
        {
            throw new InvalidOperationException("Storage:GitHubRepo:Ref is required.");
        }

        return new GitHubRepoPathResolver(
            owner: _options.Owner,
            repo: _options.Repo,
            reference: _options.Ref,
            apiBaseUrl: string.IsNullOrWhiteSpace(_options.ApiBaseUrl)
                ? GitHubRepoStorageOptions.DefaultApiBaseUrl
                : _options.ApiBaseUrl,
            rawBaseUrl: string.IsNullOrWhiteSpace(_options.RawBaseUrl)
                ? GitHubRepoStorageOptions.DefaultRawBaseUrl
                : _options.RawBaseUrl,
            basePath: _options.BasePath);
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

    private static string? NormalizeExpectedSha(string? expectedSha)
    {
        return string.IsNullOrWhiteSpace(expectedSha)
            ? null
            : expectedSha.Trim();
    }

    private sealed record GitHubPutContentRequest(
        string Message,
        string Content,
        string Branch,
        string? Sha);

    private sealed record GitHubFileMetadata(
        string Path,
        string Sha,
        string? DownloadUrl,
        string? Encoding,
        string? Base64Content);
}

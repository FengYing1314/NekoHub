namespace NekoHub.Application.Abstractions.Storage;

public interface IGitHubRepoStorage
{
    Task<IReadOnlyList<GitHubRepoDirectoryEntry>> ListDirectoryAsync(
        string? relativePath = null,
        bool recursive = false,
        int maxDepth = 2,
        CancellationToken cancellationToken = default);

    Task<GitHubRepoUpsertFileResult> UpsertFileAsync(
        Stream content,
        GitHubRepoUpsertFileRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record GitHubRepoDirectoryEntry(
    string Name,
    string RelativePath,
    bool IsDirectory,
    long? Size,
    string? Sha,
    string? PublicUrl);

public sealed record GitHubRepoUpsertFileRequest(
    string RelativePath,
    string? CommitMessage = null,
    string? ExpectedSha = null);

public sealed record GitHubRepoUpsertFileResult(
    string StorageKey,
    string RelativePath,
    string Sha,
    bool Created,
    string? PublicUrl);

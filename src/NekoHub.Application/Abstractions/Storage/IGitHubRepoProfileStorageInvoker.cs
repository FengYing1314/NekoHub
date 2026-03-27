namespace NekoHub.Application.Abstractions.Storage;

public interface IGitHubRepoProfileStorageInvoker
{
    Task<IReadOnlyList<GitHubRepoDirectoryEntry>> BrowseAsync(
        GitHubRepoProfileStorageContext context,
        string? relativePath = null,
        bool recursive = false,
        int maxDepth = 2,
        CancellationToken cancellationToken = default);

    Task<GitHubRepoUpsertFileResult> UpsertAsync(
        GitHubRepoProfileStorageContext context,
        Stream content,
        GitHubRepoUpsertFileRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record GitHubRepoProfileStorageContext(
    string Owner,
    string Repo,
    string Ref,
    string? BasePath,
    string ApiBaseUrl,
    string RawBaseUrl,
    string VisibilityPolicy,
    string? Token,
    string? CommitMessageTemplate = null);

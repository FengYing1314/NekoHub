using NekoHub.Application.Abstractions.Storage;
using NekoHub.Infrastructure.Options;

namespace NekoHub.Infrastructure.Storage.GitHub;

public sealed class GitHubRepoProfileStorageInvoker(IHttpClientFactory httpClientFactory) : IGitHubRepoProfileStorageInvoker
{
    public Task<IReadOnlyList<GitHubRepoDirectoryEntry>> BrowseAsync(
        GitHubRepoProfileStorageContext context,
        string? relativePath = null,
        bool recursive = false,
        int maxDepth = 2,
        CancellationToken cancellationToken = default)
    {
        var storage = CreateStorage(context);
        return storage.ListDirectoryAsync(relativePath, recursive, maxDepth, cancellationToken);
    }

    public Task<GitHubRepoUpsertFileResult> UpsertAsync(
        GitHubRepoProfileStorageContext context,
        Stream content,
        GitHubRepoUpsertFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var storage = CreateStorage(context);
        return storage.UpsertFileAsync(content, request, cancellationToken);
    }

    private GitHubRepoAssetStorage CreateStorage(GitHubRepoProfileStorageContext context)
    {
        var options = new GitHubRepoStorageOptions
        {
            ProviderName = GitHubRepoStorageOptions.DefaultProviderName,
            Owner = context.Owner,
            Repo = context.Repo,
            Ref = context.Ref,
            BasePath = context.BasePath,
            ApiBaseUrl = context.ApiBaseUrl,
            RawBaseUrl = context.RawBaseUrl,
            VisibilityPolicy = context.VisibilityPolicy,
            Token = context.Token,
            CommitMessageTemplate = context.CommitMessageTemplate
        };

        return new GitHubRepoAssetStorage(Microsoft.Extensions.Options.Options.Create(options), httpClientFactory);
    }
}

namespace NekoHub.Api.Contracts.Requests;

public sealed class BrowseGitHubRepoStorageProfileRequest
{
    public string? Path { get; init; }

    public bool? Recursive { get; init; }

    public int? MaxDepth { get; init; }

    public string? Type { get; init; }

    public string? Keyword { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }
}

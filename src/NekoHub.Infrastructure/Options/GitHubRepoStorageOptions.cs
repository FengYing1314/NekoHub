namespace NekoHub.Infrastructure.Options;

public sealed class GitHubRepoStorageOptions
{
    public const string DefaultProviderName = "github-repo";
    public const string SectionName = "Storage:GitHubRepo";
    public const string DefaultApiBaseUrl = "https://api.github.com";
    public const string DefaultRawBaseUrl = "https://raw.githubusercontent.com";

    public string ProviderName { get; init; } = DefaultProviderName;

    public string? Owner { get; init; }

    public string? Repo { get; init; }

    public string? Ref { get; init; } = "main";

    public string? BasePath { get; init; }

    public string ApiBaseUrl { get; init; } = DefaultApiBaseUrl;

    public string RawBaseUrl { get; init; } = DefaultRawBaseUrl;

    public string VisibilityPolicy { get; init; } = "public-only";

    public string? Token { get; init; }

    public string? CommitMessageTemplate { get; init; }
}

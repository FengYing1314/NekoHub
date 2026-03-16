namespace NekoHub.Api.Contracts.Requests;

public sealed class UpsertGitHubRepoStorageProfileRequest
{
    public string? Path { get; init; }

    public string? ContentBase64 { get; init; }

    public string? CommitMessage { get; init; }

    public string? ExpectedSha { get; init; }
}

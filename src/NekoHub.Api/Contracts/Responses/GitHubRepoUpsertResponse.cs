namespace NekoHub.Api.Contracts.Responses;

public sealed record GitHubRepoUpsertResponse(
    Guid ProfileId,
    string Path,
    string Operation,
    long Size,
    string Sha,
    string VisibilityPolicy,
    bool UsesControlledRead,
    string? PublicUrl);

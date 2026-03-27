namespace NekoHub.Api.Contracts.Responses;

public sealed record GitHubRepoBrowseResponse(
    Guid ProfileId,
    string RequestedPath,
    bool Recursive,
    int MaxDepth,
    string Type,
    string? Keyword,
    int Total,
    int Page,
    int PageSize,
    bool HasMore,
    string VisibilityPolicy,
    bool UsesControlledRead,
    IReadOnlyList<GitHubRepoBrowseItemResponse> Items);

public sealed record GitHubRepoBrowseItemResponse(
    string Name,
    string Path,
    string Type,
    bool IsDirectory,
    bool IsFile,
    long? Size,
    string? Sha,
    string? PublicUrl);

namespace NekoHub.Application.Storage.Dtos;

public sealed record GitHubRepoBrowseProfileRequestDto(
    string? Path,
    bool? Recursive,
    int? MaxDepth,
    string? Type,
    string? Keyword,
    int? Page,
    int? PageSize);

public sealed record GitHubRepoBrowseProfileResultDto(
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
    IReadOnlyList<GitHubRepoBrowseProfileEntryDto> Items);

public sealed record GitHubRepoBrowseProfileEntryDto(
    string Name,
    string Path,
    string Type,
    bool IsDirectory,
    bool IsFile,
    long? Size,
    string? Sha,
    string? PublicUrl);

public sealed record GitHubRepoUpsertProfileRequestDto(
    string? Path,
    string? ContentBase64,
    string? CommitMessage,
    string? ExpectedSha);

public sealed record GitHubRepoUpsertProfileResultDto(
    Guid ProfileId,
    string Path,
    string Operation,
    long Size,
    string Sha,
    string VisibilityPolicy,
    bool UsesControlledRead,
    string? PublicUrl);

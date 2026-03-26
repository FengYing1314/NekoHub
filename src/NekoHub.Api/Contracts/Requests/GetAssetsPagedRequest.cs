namespace NekoHub.Api.Contracts.Requests;

public sealed class GetAssetsPagedRequest
{
    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string? Query { get; init; }

    public string? Keyword { get; init; }

    public string? ContentType { get; init; }

    public string? Status { get; init; }

    public string? OrderBy { get; init; }

    public string? OrderDirection { get; init; }

    public string? SortBy { get; init; }

    public string? SortDirection { get; init; }
}

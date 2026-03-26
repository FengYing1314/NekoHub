namespace NekoHub.Api.Contracts.Requests;

public sealed class GetAssetsPagedRequest
{
    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string? Keyword { get; init; }

    public string? ContentType { get; init; }

    public string? SortBy { get; init; }

    public string? SortDirection { get; init; }
}

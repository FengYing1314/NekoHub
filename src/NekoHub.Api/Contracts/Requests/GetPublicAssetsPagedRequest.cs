namespace NekoHub.Api.Contracts.Requests;

public sealed class GetPublicAssetsPagedRequest
{
    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string? Query { get; init; }

    public string? ContentType { get; init; }
}

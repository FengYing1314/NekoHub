namespace NekoHub.Application.Assets.Queries;

public sealed record GetAssetsPagedQuery(int Page, int PageSize, int MaxPageSize);

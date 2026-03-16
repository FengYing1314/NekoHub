using NekoHub.Application.Assets.Queries;
using NekoHub.Application.Assets.Queries.Dtos;

namespace NekoHub.Application.Assets.Services;

public interface IAssetQueryService
{
    Task<AssetDetailsQueryDto> GetByIdAsync(Guid assetId, CancellationToken cancellationToken = default);

    Task<AssetPagedQueryDto> GetPagedAsync(GetAssetsPagedQuery query, CancellationToken cancellationToken = default);

    Task<PublicAssetQueryDto> GetPublicByIdAsync(Guid assetId, CancellationToken cancellationToken = default);

    Task<PublicAssetPagedQueryDto> GetPublicPagedAsync(GetAssetsPagedQuery query, CancellationToken cancellationToken = default);

    Task<AssetUsageStatsQueryDto> GetUsageStatsAsync(CancellationToken cancellationToken = default);
}

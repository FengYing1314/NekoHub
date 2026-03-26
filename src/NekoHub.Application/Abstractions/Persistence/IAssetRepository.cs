using NekoHub.Application.Common.Models;
using NekoHub.Application.Assets.Queries;
using NekoHub.Domain.Assets;

namespace NekoHub.Application.Abstractions.Persistence;

public interface IAssetRepository
{
    Task AddAsync(Asset asset, CancellationToken cancellationToken = default);

    Task<Asset?> GetByIdAsync(Guid assetId, CancellationToken cancellationToken = default);

    Task<PagedResult<Asset>> GetPagedAsync(GetAssetsPagedQuery query, CancellationToken cancellationToken = default);

    Task DeleteAsync(Asset asset, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

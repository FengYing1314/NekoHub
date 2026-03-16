using NekoHub.Domain.Assets;

namespace NekoHub.Application.Abstractions.Persistence;

public interface IAssetDerivativeRepository
{
    Task<AssetDerivative?> GetBySourceAndKindAsync(
        Guid sourceAssetId,
        string kind,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetDerivative>> GetBySourceAssetIdAsync(
        Guid sourceAssetId,
        CancellationToken cancellationToken = default);

    Task AddAsync(AssetDerivative derivative, CancellationToken cancellationToken = default);

    Task DeleteRangeAsync(IEnumerable<AssetDerivative> derivatives, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

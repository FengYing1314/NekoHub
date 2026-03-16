using NekoHub.Domain.Assets;

namespace NekoHub.Application.Abstractions.Persistence;

public interface IAssetStructuredResultRepository
{
    Task<AssetStructuredResult?> GetBySourceAndKindAsync(
        Guid sourceAssetId,
        string kind,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetStructuredResult>> GetBySourceAssetIdAsync(
        Guid sourceAssetId,
        CancellationToken cancellationToken = default);

    Task AddAsync(AssetStructuredResult result, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

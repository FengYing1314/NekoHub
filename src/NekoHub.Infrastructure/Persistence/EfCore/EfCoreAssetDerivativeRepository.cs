using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Domain.Assets;

namespace NekoHub.Infrastructure.Persistence.EfCore;

public sealed class EfCoreAssetDerivativeRepository(AssetDbContext dbContext) : IAssetDerivativeRepository
{
    public Task<AssetDerivative?> GetBySourceAndKindAsync(
        Guid sourceAssetId,
        string kind,
        CancellationToken cancellationToken = default)
    {
        return dbContext.AssetDerivatives
            .SingleOrDefaultAsync(
                derivative => derivative.SourceAssetId == sourceAssetId && derivative.Kind == kind,
                cancellationToken);
    }

    public async Task<IReadOnlyList<AssetDerivative>> GetBySourceAssetIdAsync(
        Guid sourceAssetId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.AssetDerivatives
            .Where(derivative => derivative.SourceAssetId == sourceAssetId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AssetDerivative derivative, CancellationToken cancellationToken = default)
    {
        await dbContext.AssetDerivatives.AddAsync(derivative, cancellationToken);
    }

    public Task DeleteRangeAsync(IEnumerable<AssetDerivative> derivatives, CancellationToken cancellationToken = default)
    {
        dbContext.AssetDerivatives.RemoveRange(derivatives);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Domain.Assets;

namespace NekoHub.Infrastructure.Persistence.EfCore;

public sealed class EfCoreAssetStructuredResultRepository(AssetDbContext dbContext) : IAssetStructuredResultRepository
{
    public Task<AssetStructuredResult?> GetBySourceAndKindAsync(
        Guid sourceAssetId,
        string kind,
        CancellationToken cancellationToken = default)
    {
        return dbContext.AssetStructuredResults
            .SingleOrDefaultAsync(
                result => result.SourceAssetId == sourceAssetId && result.Kind == kind,
                cancellationToken);
    }

    public async Task<IReadOnlyList<AssetStructuredResult>> GetBySourceAssetIdAsync(
        Guid sourceAssetId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.AssetStructuredResults
            .Where(result => result.SourceAssetId == sourceAssetId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AssetStructuredResult result, CancellationToken cancellationToken = default)
    {
        await dbContext.AssetStructuredResults.AddAsync(result, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

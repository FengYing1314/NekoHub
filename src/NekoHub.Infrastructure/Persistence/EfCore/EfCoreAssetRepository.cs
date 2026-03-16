using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Common.Models;
using NekoHub.Domain.Assets;

namespace NekoHub.Infrastructure.Persistence.EfCore;

public sealed class EfCoreAssetRepository(AssetDbContext dbContext) : IAssetRepository
{
    public async Task AddAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        await dbContext.Assets.AddAsync(asset, cancellationToken);
    }

    public Task<Asset?> GetByIdAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        return dbContext.Assets
            .SingleOrDefaultAsync(x => x.Id == assetId, cancellationToken);
    }

    public async Task<PagedResult<Asset>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var safePage = page <= 0 ? 1 : page;
        var safePageSize = pageSize <= 0 ? 20 : pageSize;

        var query = dbContext.Assets
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Asset>(items, safePage, safePageSize, total);
    }

    public Task DeleteAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        dbContext.Assets.Remove(asset);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

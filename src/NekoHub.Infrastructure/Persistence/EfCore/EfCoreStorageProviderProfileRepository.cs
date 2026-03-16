using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Domain.Storage;

namespace NekoHub.Infrastructure.Persistence.EfCore;

public sealed class EfCoreStorageProviderProfileRepository(AssetDbContext dbContext) : IStorageProviderProfileRepository
{
    public async Task AddAsync(StorageProviderProfile profile, CancellationToken cancellationToken = default)
    {
        await dbContext.StorageProviderProfiles.AddAsync(profile, cancellationToken);
    }

    public Task<StorageProviderProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        return dbContext.StorageProviderProfiles
            .SingleOrDefaultAsync(x => x.Id == profileId, cancellationToken);
    }

    public Task<StorageProviderProfile?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return dbContext.StorageProviderProfiles
            .SingleOrDefaultAsync(x => x.Name == name, cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(
        string name,
        Guid? excludeProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToLower();

        return dbContext.StorageProviderProfiles.AnyAsync(
            x => x.Name.ToLower() == normalizedName
                 && (!excludeProfileId.HasValue || x.Id != excludeProfileId.Value),
            cancellationToken);
    }

    public Task<StorageProviderProfile?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.StorageProviderProfiles
            .Where(x => x.IsDefault)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StorageProviderProfile>> ListDefaultProfilesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.StorageProviderProfiles
            .Where(x => x.IsDefault)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StorageProviderProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.StorageProviderProfiles
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.IsEnabled)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteAsync(StorageProviderProfile profile, CancellationToken cancellationToken = default)
    {
        dbContext.StorageProviderProfiles.Remove(profile);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

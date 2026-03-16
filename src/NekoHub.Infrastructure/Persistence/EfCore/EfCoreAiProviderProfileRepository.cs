using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Domain.Ai;

namespace NekoHub.Infrastructure.Persistence.EfCore;

public sealed class EfCoreAiProviderProfileRepository(AssetDbContext dbContext) : IAiProviderProfileRepository
{
    public async Task AddAsync(AiProviderProfile profile, CancellationToken cancellationToken = default)
    {
        await dbContext.AiProviderProfiles.AddAsync(profile, cancellationToken);
    }

    public Task<AiProviderProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        return dbContext.AiProviderProfiles
            .SingleOrDefaultAsync(x => x.Id == profileId, cancellationToken);
    }

    public Task<AiProviderProfile?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return dbContext.AiProviderProfiles
            .SingleOrDefaultAsync(x => x.Name == name, cancellationToken);
    }

    public Task<AiProviderProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.AiProviderProfiles
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(
        string name,
        Guid? excludeProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToLower();

        return dbContext.AiProviderProfiles.AnyAsync(
            x => x.Name.ToLower() == normalizedName
                 && (!excludeProfileId.HasValue || x.Id != excludeProfileId.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AiProviderProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.AiProviderProfiles
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiProviderProfile>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.AiProviderProfiles
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteAsync(AiProviderProfile profile, CancellationToken cancellationToken = default)
    {
        dbContext.AiProviderProfiles.Remove(profile);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

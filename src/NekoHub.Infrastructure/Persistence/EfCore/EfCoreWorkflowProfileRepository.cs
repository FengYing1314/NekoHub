using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Domain.Workflows;

namespace NekoHub.Infrastructure.Persistence.EfCore;

public sealed class EfCoreWorkflowProfileRepository(AssetDbContext dbContext) : IWorkflowProfileRepository
{
    public async Task AddAsync(WorkflowProfile profile, CancellationToken cancellationToken = default)
    {
        await dbContext.WorkflowProfiles.AddAsync(profile, cancellationToken);
    }

    public Task<WorkflowProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        return dbContext.WorkflowProfiles
            .SingleOrDefaultAsync(x => x.Id == profileId, cancellationToken);
    }

    public Task<WorkflowProfile?> GetAutoRunAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.WorkflowProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.IsAutoRun, cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(
        string name,
        Guid? excludeProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim().ToLower();

        return dbContext.WorkflowProfiles.AnyAsync(
            x => x.Name.ToLower() == normalizedName
                 && (!excludeProfileId.HasValue || x.Id != excludeProfileId.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowProfiles
            .AsNoTracking()
            .OrderByDescending(x => x.IsAutoRun)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowProfile>> ListAutoRunAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowProfiles
            .Where(x => x.IsAutoRun)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteAsync(WorkflowProfile profile, CancellationToken cancellationToken = default)
    {
        dbContext.WorkflowProfiles.Remove(profile);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Domain.Skills;

namespace NekoHub.Infrastructure.Persistence.EfCore;

public sealed class EfCoreAssetSkillExecutionRepository(AssetDbContext dbContext) : IAssetSkillExecutionRepository
{
    public async Task AddExecutionAsync(SkillExecution execution, CancellationToken cancellationToken = default)
    {
        await dbContext.SkillExecutions.AddAsync(execution, cancellationToken);
    }

    public async Task AddStepResultsAsync(
        IEnumerable<SkillExecutionStepResult> stepResults,
        CancellationToken cancellationToken = default)
    {
        await dbContext.SkillExecutionStepResults.AddRangeAsync(stepResults, cancellationToken);
    }

    public Task<SkillExecution?> GetLatestBySourceAssetIdAsync(
        Guid sourceAssetId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.SkillExecutions
            .AsNoTracking()
            .Where(execution => execution.SourceAssetId == sourceAssetId)
            .OrderByDescending(execution => execution.StartedAtUtc)
            .ThenByDescending(execution => execution.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SkillExecutionStepResult>> GetByExecutionIdAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.SkillExecutionStepResults
            .AsNoTracking()
            .Where(step => step.SkillExecutionId == executionId)
            .OrderBy(step => step.StartedAtUtc)
            .ThenBy(step => step.StepName)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

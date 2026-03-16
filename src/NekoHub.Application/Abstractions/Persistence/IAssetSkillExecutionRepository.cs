using NekoHub.Domain.Skills;

namespace NekoHub.Application.Abstractions.Persistence;

public interface IAssetSkillExecutionRepository
{
    Task AddExecutionAsync(SkillExecution execution, CancellationToken cancellationToken = default);

    Task AddStepResultsAsync(IEnumerable<SkillExecutionStepResult> stepResults, CancellationToken cancellationToken = default);

    Task<SkillExecution?> GetLatestBySourceAssetIdAsync(Guid sourceAssetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SkillExecutionStepResult>> GetByExecutionIdAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

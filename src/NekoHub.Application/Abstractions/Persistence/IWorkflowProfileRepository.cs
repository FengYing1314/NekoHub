using NekoHub.Domain.Workflows;

namespace NekoHub.Application.Abstractions.Persistence;

public interface IWorkflowProfileRepository
{
    Task AddAsync(WorkflowProfile profile, CancellationToken cancellationToken = default);

    Task<WorkflowProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<WorkflowProfile?> GetAutoRunAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, Guid? excludeProfileId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowProfile>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowProfile>> ListAutoRunAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(WorkflowProfile profile, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

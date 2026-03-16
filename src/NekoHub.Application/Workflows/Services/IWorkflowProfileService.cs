using NekoHub.Application.Workflows.Dtos;

namespace NekoHub.Application.Workflows.Services;

public interface IWorkflowProfileService
{
    Task<IReadOnlyList<WorkflowProfileDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<WorkflowProfileDto> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<WorkflowProfileDto> CreateAsync(CreateWorkflowRequest request, CancellationToken cancellationToken = default);

    Task<WorkflowProfileDto> UpdateAsync(Guid profileId, UpdateWorkflowRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<WorkflowProfileDto> SetAutoRunAsync(Guid profileId, CancellationToken cancellationToken = default);
}

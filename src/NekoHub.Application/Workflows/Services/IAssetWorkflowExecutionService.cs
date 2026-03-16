using NekoHub.Application.Workflows.Dtos;

namespace NekoHub.Application.Workflows.Services;

public interface IAssetWorkflowExecutionService
{
    Task<QueuedWorkflowRunResultDto> QueueWorkflowAsync(
        Guid assetId,
        Guid workflowId,
        CancellationToken cancellationToken = default);
}

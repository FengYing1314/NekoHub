using NekoHub.Domain.Workflows;
using NekoHub.Application.Workflows.Dtos;

namespace NekoHub.Application.Workflows;

internal static class WorkflowProfileMapper
{
    public static WorkflowProfileDto ToDto(WorkflowProfile profile)
    {
        return new WorkflowProfileDto(
            Id: profile.Id,
            Name: profile.Name,
            Description: profile.Description,
            IsAutoRun: profile.IsAutoRun,
            GraphJson: profile.GraphJson,
            CreatedAtUtc: profile.CreatedAtUtc,
            UpdatedAtUtc: profile.UpdatedAtUtc);
    }
}

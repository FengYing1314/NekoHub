namespace NekoHub.Application.Workflows.Dtos;

public sealed record QueuedWorkflowRunResultDto(
    Guid AssetId,
    Guid WorkflowId,
    IReadOnlyList<string> SkillIds);

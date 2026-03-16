namespace NekoHub.Application.Workflows.Dtos;

public sealed record UpdateWorkflowRequest(
    string? Name,
    string? Description,
    bool IsAutoRun,
    string? GraphJson);

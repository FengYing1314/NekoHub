namespace NekoHub.Application.Workflows.Dtos;

public sealed record CreateWorkflowRequest(
    string? Name,
    string? Description,
    bool IsAutoRun,
    string? GraphJson);

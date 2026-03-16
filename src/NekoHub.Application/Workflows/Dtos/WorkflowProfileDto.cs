namespace NekoHub.Application.Workflows.Dtos;

public sealed record WorkflowProfileDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsAutoRun,
    string GraphJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

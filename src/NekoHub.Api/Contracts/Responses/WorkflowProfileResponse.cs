namespace NekoHub.Api.Contracts.Responses;

public sealed record WorkflowProfileResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsAutoRun,
    string GraphJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

namespace NekoHub.Api.Contracts.Responses;

public sealed record AssetLatestExecutionStepSummaryResponse(
    string StepName,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

public sealed record AssetLatestExecutionSummaryResponse(
    Guid ExecutionId,
    string SkillName,
    string TriggerSource,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    bool Succeeded,
    IReadOnlyList<AssetLatestExecutionStepSummaryResponse> Steps);

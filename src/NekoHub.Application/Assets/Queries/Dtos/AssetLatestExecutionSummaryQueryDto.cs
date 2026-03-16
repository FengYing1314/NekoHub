namespace NekoHub.Application.Assets.Queries.Dtos;

public sealed record AssetLatestExecutionStepSummaryQueryDto(
    string StepName,
    bool Succeeded,
    string? ErrorMessage,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

public sealed record AssetLatestExecutionSummaryQueryDto(
    Guid ExecutionId,
    string SkillName,
    string TriggerSource,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    bool Succeeded,
    IReadOnlyList<AssetLatestExecutionStepSummaryQueryDto> Steps);

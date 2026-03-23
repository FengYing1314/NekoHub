namespace NekoHub.Application.Abstractions.Skills;

public sealed record SkillStepRunResult(
    string Name,
    bool Succeeded,
    string? ErrorMessage = null);

public sealed record SkillRunResult(
    string SkillName,
    bool Succeeded,
    IReadOnlyList<SkillStepRunResult> Steps);

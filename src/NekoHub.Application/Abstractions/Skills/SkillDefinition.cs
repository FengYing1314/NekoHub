namespace NekoHub.Application.Abstractions.Skills;

public sealed record SkillDefinition(
    string Name,
    string Description,
    IReadOnlyList<SkillStep> Steps,
    int Order = 0);

namespace NekoHub.Application.Skills.Dtos;

public sealed record AssetSkillSummaryDto(
    string SkillName,
    string Description,
    IReadOnlyList<string> Steps);

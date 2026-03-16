using NekoHub.Application.Assets.Queries.Dtos;

namespace NekoHub.Application.Skills.Dtos;

public sealed record RunAssetSkillStepResultDto(
    string Name,
    bool Succeeded,
    string? ErrorMessage);

public sealed record RunAssetSkillResultDto(
    bool Succeeded,
    string SkillName,
    IReadOnlyList<RunAssetSkillStepResultDto> Steps,
    AssetDetailsQueryDto Asset);

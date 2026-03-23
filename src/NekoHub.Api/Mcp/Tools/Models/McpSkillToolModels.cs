using NekoHub.Application.Skills.Dtos;

namespace NekoHub.Api.Mcp.Tools.Models;

public sealed record McpSkillView(
    string SkillName,
    string Description,
    IReadOnlyList<string> Steps);

public sealed record McpSkillListView(IReadOnlyList<McpSkillView> Skills);

public sealed record McpRunAssetSkillStepView(
    string Name,
    bool Succeeded,
    string? ErrorMessage);

public sealed record McpRunAssetSkillView(
    bool Succeeded,
    string SkillName,
    IReadOnlyList<McpRunAssetSkillStepView> Steps,
    McpAssetView Asset);

public static class McpSkillToolModelMapper
{
    public static McpSkillListView ToView(IReadOnlyList<AssetSkillSummaryDto> skills)
    {
        return new McpSkillListView(
            Skills: skills
                .Select(static skill => new McpSkillView(
                    SkillName: skill.SkillName,
                    Description: skill.Description,
                    Steps: skill.Steps))
                .ToList());
    }

    public static McpRunAssetSkillView ToView(RunAssetSkillResultDto result)
    {
        return new McpRunAssetSkillView(
            Succeeded: result.Succeeded,
            SkillName: result.SkillName,
            Steps: result.Steps
                .Select(static step => new McpRunAssetSkillStepView(
                    Name: step.Name,
                    Succeeded: step.Succeeded,
                    ErrorMessage: step.ErrorMessage))
                .ToList(),
            Asset: McpAssetToolModelMapper.ToView(result.Asset));
    }
}

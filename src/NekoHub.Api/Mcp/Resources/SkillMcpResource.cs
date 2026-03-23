using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Application.Skills.Services;

namespace NekoHub.Api.Mcp.Resources;

public sealed class SkillMcpResource(IAssetSkillService assetSkillService) : IMcpResource
{
    private static readonly McpResourceDescriptor SkillTemplateDescriptor = new(
        Uri: "skill://{name}",
        Name: "skill_detail")
    {
        Title = "Skill Detail",
        Description = "Read skill metadata including description and steps.",
        MimeType = "application/json"
    };

    public async Task<IReadOnlyList<McpResourceDescriptor>> ListAsync(CancellationToken cancellationToken)
    {
        var skills = await assetSkillService.ListAsync(cancellationToken);
        var descriptors = new List<McpResourceDescriptor> { SkillTemplateDescriptor };
        descriptors.AddRange(skills.Select(skill => new McpResourceDescriptor(
            Uri: $"skill://{skill.SkillName}",
            Name: "skill_detail")
        {
            Title = skill.SkillName,
            Description = skill.Description,
            MimeType = "application/json"
        }));

        return descriptors;
    }

    public bool CanHandle(Uri resourceUri)
    {
        return string.Equals(resourceUri.Scheme, "skill", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<McpResourceReadResult> ReadAsync(Uri resourceUri, CancellationToken cancellationToken)
    {
        var skillName = ParseSkillName(resourceUri);
        var path = resourceUri.AbsolutePath.Trim('/');
        if (!string.IsNullOrWhiteSpace(path))
        {
            throw new NotFoundException(
                "resource_not_found",
                $"Resource '{resourceUri}' was not found.");
        }

        var skills = await assetSkillService.ListAsync(cancellationToken);
        var skill = skills.FirstOrDefault(definition =>
            string.Equals(definition.SkillName, skillName, StringComparison.OrdinalIgnoreCase));

        if (skill is null)
        {
            throw new NotFoundException(
                "skill_not_found",
                $"Skill '{skillName}' was not found.");
        }

        var view = new McpSkillView(skill.SkillName, skill.Description, skill.Steps);
        return new McpResourceReadResult(resourceUri.ToString(), view);
    }

    private static string ParseSkillName(Uri resourceUri)
    {
        var skillName = Uri.UnescapeDataString(resourceUri.Host);
        if (string.IsNullOrWhiteSpace(skillName))
        {
            throw new ValidationException(
                "resource_uri_invalid",
                $"Resource uri '{resourceUri}' is missing skill name.");
        }

        return skillName;
    }
}

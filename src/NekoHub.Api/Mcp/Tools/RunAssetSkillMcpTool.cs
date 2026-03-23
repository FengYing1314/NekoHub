using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Application.Skills.Services;

namespace NekoHub.Api.Mcp.Tools;

public sealed class RunAssetSkillMcpTool(IAssetSkillService assetSkillService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "run_asset_skill",
        InputSchema: new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["assetId"] = new
                {
                    type = "string",
                    format = "uuid"
                },
                ["skillName"] = new
                {
                    type = "string"
                }
            },
            required = new[] { "assetId", "skillName" },
            additionalProperties = false
        })
    {
        Title = "Run Asset Skill",
        Description = "Run a named skill for a target asset and return step outcomes plus latest asset summary.",
        OutputSchema = McpSkillToolSchemas.RunAssetSkill,
        Annotations = new McpToolAnnotations
        {
            ReadOnlyHint = false,
            DestructiveHint = false,
            IdempotentHint = false,
            OpenWorldHint = false
        }
    };

    public async Task<McpToolInvocationResult> InvokeAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        var input = McpToolArgumentParser.ParseRequired<Arguments>(arguments, Definition.Name);
        if (!Guid.TryParse(input.AssetId, out var assetId))
        {
            throw new ValidationException("tool_argument_invalid", "Argument 'assetId' must be a valid GUID.");
        }

        var runResult = await assetSkillService.RunAsync(assetId, input.SkillName ?? string.Empty, cancellationToken);
        return new McpToolInvocationResult(McpSkillToolModelMapper.ToView(runResult));
    }

    private sealed record Arguments(string? AssetId, string? SkillName);
}

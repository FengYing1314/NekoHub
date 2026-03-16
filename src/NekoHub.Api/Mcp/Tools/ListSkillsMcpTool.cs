using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Skills.Services;

namespace NekoHub.Api.Mcp.Tools;

public sealed class ListSkillsMcpTool(IAssetSkillService assetSkillService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "list_skills",
        InputSchema: new
        {
            type = "object",
            additionalProperties = false
        })
    {
        Title = "List Skills",
        Description = "List available skill definitions with descriptions and step sequence for agent discovery.",
        OutputSchema = McpSkillToolSchemas.SkillList,
        Annotations = new McpToolAnnotations
        {
            ReadOnlyHint = true,
            DestructiveHint = false,
            IdempotentHint = true,
            OpenWorldHint = false
        }
    };

    public async Task<McpToolInvocationResult> InvokeAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        _ = McpToolArgumentParser.ParseOptional<NoArguments>(arguments, Definition.Name);
        var skills = await assetSkillService.ListAsync(cancellationToken);
        return new McpToolInvocationResult(McpSkillToolModelMapper.ToView(skills));
    }

    private sealed record NoArguments;
}

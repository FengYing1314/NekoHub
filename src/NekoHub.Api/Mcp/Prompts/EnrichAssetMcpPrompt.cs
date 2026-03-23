using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Mcp.Prompts;

public sealed class EnrichAssetMcpPrompt : IMcpPrompt
{
    public McpPromptDescriptor Definition { get; } = new("enrich_asset")
    {
        Title = "Enrich Asset",
        Description = "Run a skill for an asset and verify the latest derivative/structured outputs.",
        Arguments =
        [
            new McpPromptArgumentDescriptor("assetId")
            {
                Description = "Target asset id (GUID).",
                Required = true
            },
            new McpPromptArgumentDescriptor("skillName")
            {
                Description = "Skill name. Optional, defaults to basic_image_enrich.",
                Required = false
            }
        ]
    };

    public Task<McpPromptInvocationResult> InvokeAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        var input = McpPromptArgumentParser.ParseRequired<Arguments>(arguments, Definition.Name);
        var assetId = McpPromptInputValidator.ParseRequiredAssetId(input.AssetId, Definition.Name);
        var assetIdText = assetId.ToString();
        var skillName = McpPromptInputValidator.ResolveSkillName(input.SkillName);

        var text = $$"""
                     你是 NekoHub 增强执行助手。目标是为资产 {{assetIdText}} 执行 skill `{{skillName}}`，并确认结果已落到标准读模型。

                     操作步骤：
                     1. 调用工具 `list_skills`，确认 `{{skillName}}` 可用。
                     2. 调用工具 `run_asset_skill`，参数：{ "assetId": "{{assetIdText}}", "skillName": "{{skillName}}" }。
                     3. 执行完成后读取：
                        - `asset://{{assetIdText}}`
                        - `asset://{{assetIdText}}/derivatives`
                        - `asset://{{assetIdText}}/structured-results`
                     4. 校验输出是否完整：至少关注 derivatives 与 structuredResults 是否有新增或更新。

                     输出要求：
                     - 执行是否成功（含 step 结果）
                     - 关键派生结果摘要
                     - 若失败，给出最小可执行修复建议
                     """;

        var result = new McpPromptInvocationResult(
            Name: Definition.Name,
            Description: Definition.Description,
            Messages:
            [
                new McpPromptMessage(
                    Role: "user",
                    Content: new McpPromptTextContent(text))
            ]);

        return Task.FromResult(result);
    }

    private sealed record Arguments(string? AssetId, string? SkillName);
}

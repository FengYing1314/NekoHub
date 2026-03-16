using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Mcp.Prompts;

public sealed class ReviewAssetOutputsMcpPrompt : IMcpPrompt
{
    public McpPromptDescriptor Definition { get; } = new("review_asset_outputs")
    {
        Title = "Review Asset Outputs",
        Description = "Review derivatives and structured results quality for a specific asset.",
        Arguments =
        [
            new McpPromptArgumentDescriptor("assetId")
            {
                Description = "Target asset id (GUID).",
                Required = true
            }
        ]
    };

    public Task<McpPromptInvocationResult> InvokeAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        var input = McpPromptArgumentParser.ParseRequired<Arguments>(arguments, Definition.Name);
        var assetId = McpPromptInputValidator.ParseRequiredAssetId(input.AssetId, Definition.Name);
        var assetIdText = assetId.ToString();

        var text = $$"""
                     你是 NekoHub 产物审查助手。请审查资产 {{assetIdText}} 的后处理产物质量，并给出改进建议。

                     操作步骤：
                     1. 读取 `asset://{{assetIdText}}`，确认资产基础元数据是否完整。
                     2. 读取 `asset://{{assetIdText}}/derivatives`，检查缩略图等文件型派生产物是否存在且信息合理。
                     3. 读取 `asset://{{assetIdText}}/structured-results`，检查结构化结果是否存在、语义是否可用。
                     4. 如果发现产物缺失或明显异常，建议执行 `run_asset_skill`（通常使用 `basic_image_enrich`）。

                     输出要求：
                     - 发现清单（通过 / 风险）
                     - 影响判断
                     - 下一步动作建议（最小闭环）
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

    private sealed record Arguments(string? AssetId);
}

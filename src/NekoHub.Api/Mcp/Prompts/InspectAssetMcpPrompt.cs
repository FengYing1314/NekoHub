using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;

namespace NekoHub.Api.Mcp.Prompts;

public sealed class InspectAssetMcpPrompt : IMcpPrompt
{
    public McpPromptDescriptor Definition { get; } = new("inspect_asset")
    {
        Title = "Inspect Asset",
        Description = "Inspect asset metadata and processing outputs through stable tool/resource surfaces.",
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
                     你是 NekoHub 资产分析助手。请围绕资产 {{assetIdText}} 执行一次可复现检查，严格复用现有平台能力。

                     操作步骤：
                     1. 调用工具 `get_asset`，参数：{ "id": "{{assetIdText}}" }。
                     2. 读取资源 `asset://{{assetIdText}}`，必要时补充读取：
                        - `asset://{{assetIdText}}/derivatives`
                        - `asset://{{assetIdText}}/structured-results`
                     3. 汇总字段：contentType、extension、size、checksumSha256、width、height、derivatives、structuredResults。

                     输出要求：
                     - 资产概览（关键元数据）
                     - 当前处理产物（文件型 + 结构化）
                     - 下一步建议（是否需要执行 `run_asset_skill`）
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

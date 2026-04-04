using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Storage.Services;

namespace NekoHub.Api.Mcp.Tools;

public sealed class ListStorageProfilesMcpTool(IStorageProviderQueryService storageProviderQueryService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "list_storage_profiles",
        InputSchema: new
        {
            type = "object",
            additionalProperties = false
        })
    {
        Title = "List Storage Profiles",
        Description = "List safe storage profile summaries for agent routing and profile selection without exposing secrets.",
        OutputSchema = McpStorageProfileToolSchemas.StorageProfileList,
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
        var overview = await storageProviderQueryService.GetOverviewAsync(cancellationToken);
        return new McpToolInvocationResult(McpStorageProfileToolModelMapper.ToView(overview.Profiles));
    }

    private sealed record NoArguments;
}

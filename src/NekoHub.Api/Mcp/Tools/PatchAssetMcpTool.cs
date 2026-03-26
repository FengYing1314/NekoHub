using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Assets.Commands;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Mcp.Tools;

public sealed class PatchAssetMcpTool(
    IAssetCommandService assetCommandService,
    IAssetQueryService assetQueryService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "patch_asset",
        InputSchema: new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["id"] = new
                {
                    type = "string",
                    format = "uuid"
                },
                ["description"] = McpAssetToolSchemas.NullableString,
                ["altText"] = McpAssetToolSchemas.NullableString,
                ["originalFileName"] = McpAssetToolSchemas.NullableString
            },
            required = new[] { "id" },
            additionalProperties = false
        })
    {
        Title = "Patch Asset",
        Description = "Patch asset metadata and return the full updated asset detail read model.",
        OutputSchema = McpAssetToolSchemas.AssetDetail,
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
        if (!Guid.TryParse(input.Id, out var assetId))
        {
            throw new ValidationException("tool_argument_invalid", "Argument 'id' must be a valid GUID.");
        }

        await assetCommandService.PatchAsync(
            new PatchAssetMetadataCommand(
                AssetId: assetId,
                Description: input.Description,
                AltText: input.AltText,
                OriginalFileName: input.OriginalFileName),
            cancellationToken);

        var asset = await assetQueryService.GetByIdAsync(assetId, cancellationToken);
        return new McpToolInvocationResult(McpAssetToolModelMapper.ToView(asset));
    }

    private sealed record Arguments(
        string? Id,
        NekoHub.Application.Common.Models.OptionalValue<string?> Description,
        NekoHub.Application.Common.Models.OptionalValue<string?> AltText,
        NekoHub.Application.Common.Models.OptionalValue<string?> OriginalFileName);
}

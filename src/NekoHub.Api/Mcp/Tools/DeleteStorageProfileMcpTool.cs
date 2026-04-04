using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Storage.Services;

namespace NekoHub.Api.Mcp.Tools;

public sealed class DeleteStorageProfileMcpTool(
    IStorageProviderProfileManagementService storageProviderProfileManagementService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "delete_storage_profile",
        InputSchema: new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["id"] = new
                {
                    type = "string",
                    format = "uuid"
                }
            },
            required = new[] { "id" },
            additionalProperties = false
        })
    {
        Title = "Delete Storage Profile",
        Description = "Delete a storage provider profile by id.",
        OutputSchema = McpStorageProfileToolSchemas.DeleteStorageProfile,
        Annotations = new McpToolAnnotations
        {
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false,
            OpenWorldHint = false
        }
    };

    public async Task<McpToolInvocationResult> InvokeAsync(JsonElement? arguments, CancellationToken cancellationToken)
    {
        var input = McpToolArgumentParser.ParseRequired<Arguments>(arguments, Definition.Name);
        var profileId = McpToolInputValidator.ParseRequiredGuid(input.Id, "id");
        var deleted = await storageProviderProfileManagementService.DeleteAsync(profileId, cancellationToken);
        return new McpToolInvocationResult(McpStorageProfileToolModelMapper.ToView(deleted));
    }

    private sealed record Arguments(string? Id);
}

using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Common.Models;
using NekoHub.Application.Storage.Commands;
using NekoHub.Application.Storage.Services;

namespace NekoHub.Api.Mcp.Tools;

public sealed class UpdateStorageProfileMcpTool(
    IStorageProviderProfileManagementService storageProviderProfileManagementService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "update_storage_profile",
        InputSchema: new
        {
            type = "object",
            properties = new Dictionary<string, object?>
            {
                ["id"] = new
                {
                    type = "string",
                    format = "uuid"
                },
                ["name"] = new
                {
                    type = "string",
                    minLength = 1
                },
                ["displayName"] = McpAssetToolSchemas.NullableString,
                ["isEnabled"] = new
                {
                    type = "boolean"
                },
                ["configuration"] = new
                {
                    type = "object",
                    additionalProperties = true
                },
                ["secretConfiguration"] = McpStorageProfileToolSchemas.NullableObject,
                ["setAsDefault"] = new
                {
                    type = "boolean"
                }
            },
            required = new[] { "id" },
            additionalProperties = false
        })
    {
        Title = "Update Storage Profile",
        Description = "Update a storage provider profile and optionally promote it as the default write profile.",
        OutputSchema = McpStorageProfileToolSchemas.StorageProfile,
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
        var profileId = McpToolInputValidator.ParseRequiredGuid(input.Id, "id");

        var updated = await storageProviderProfileManagementService.UpdateAsync(
            new UpdateStorageProviderProfileCommand(
                ProfileId: profileId,
                Name: input.Name,
                DisplayName: input.DisplayName,
                IsEnabled: input.IsEnabled,
                Configuration: input.Configuration,
                SecretConfiguration: input.SecretConfiguration),
            cancellationToken);

        if (input.SetAsDefault is true)
        {
            updated = await storageProviderProfileManagementService.SetDefaultAsync(profileId, cancellationToken);
        }

        return new McpToolInvocationResult(McpStorageProfileToolModelMapper.ToView(updated));
    }

    private sealed record Arguments(
        string? Id,
        OptionalValue<string?> Name,
        OptionalValue<string?> DisplayName,
        OptionalValue<bool> IsEnabled,
        OptionalValue<JsonElement?> Configuration,
        OptionalValue<JsonElement?> SecretConfiguration,
        bool? SetAsDefault);
}

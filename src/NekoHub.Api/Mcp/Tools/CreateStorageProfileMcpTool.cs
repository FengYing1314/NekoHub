using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Api.Mcp.Tools.Models;
using NekoHub.Application.Storage.Commands;
using NekoHub.Application.Storage.Services;

namespace NekoHub.Api.Mcp.Tools;

public sealed class CreateStorageProfileMcpTool(
    IStorageProviderProfileManagementService storageProviderProfileManagementService) : IMcpTool
{
    public McpToolDescriptor Definition { get; } = new(
        Name: "create_storage_profile",
        InputSchema: new
        {
            type = "object",
            properties = new Dictionary<string, object?>
            {
                ["name"] = new
                {
                    type = "string",
                    minLength = 1
                },
                ["displayName"] = McpAssetToolSchemas.NullableString,
                ["providerType"] = new
                {
                    type = "string",
                    minLength = 1
                },
                ["isEnabled"] = new
                {
                    type = "boolean"
                },
                ["isDefault"] = new
                {
                    type = "boolean"
                },
                ["configuration"] = new
                {
                    type = "object",
                    additionalProperties = true
                },
                ["secretConfiguration"] = McpStorageProfileToolSchemas.NullableObject
            },
            required = new[] { "name", "providerType", "configuration" },
            additionalProperties = false
        })
    {
        Title = "Create Storage Profile",
        Description = "Create a storage provider profile using configuration and optional secret configuration. Responses only return safe summaries.",
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
        var created = await storageProviderProfileManagementService.CreateAsync(
            new CreateStorageProviderProfileCommand(
                Name: input.Name,
                DisplayName: input.DisplayName,
                ProviderType: input.ProviderType,
                IsEnabled: input.IsEnabled ?? true,
                IsDefault: input.IsDefault ?? false,
                Configuration: input.Configuration,
                SecretConfiguration: input.SecretConfiguration),
            cancellationToken);

        return new McpToolInvocationResult(McpStorageProfileToolModelMapper.ToView(created));
    }

    private sealed record Arguments(
        string? Name,
        string? DisplayName,
        string? ProviderType,
        bool? IsEnabled,
        bool? IsDefault,
        JsonElement? Configuration,
        JsonElement? SecretConfiguration);
}

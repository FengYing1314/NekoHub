using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Mcp.Prompts;

internal static class McpPromptArgumentParser
{
    public static T ParseRequired<T>(JsonElement? arguments, string promptName)
    {
        if (arguments is not { ValueKind: JsonValueKind.Object } argumentObject)
        {
            throw new ValidationException(
                "prompt_arguments_invalid",
                $"Prompt '{promptName}' requires an object 'arguments' payload.");
        }

        return Deserialize<T>(argumentObject, promptName);
    }

    private static T Deserialize<T>(JsonElement arguments, string promptName)
    {
        try
        {
            return arguments.Deserialize<T>(McpJsonOptions.Default)
                   ?? throw new ValidationException(
                       "prompt_arguments_invalid",
                       $"Prompt '{promptName}' arguments payload is invalid.");
        }
        catch (JsonException exception)
        {
            throw new ValidationException(
                "prompt_arguments_invalid",
                $"Prompt '{promptName}' arguments payload is invalid: {exception.Message}");
        }
    }
}

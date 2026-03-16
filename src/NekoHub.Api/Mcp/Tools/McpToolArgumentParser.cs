using System.Text.Json;
using NekoHub.Api.Mcp.Protocol;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Mcp.Tools;

internal static class McpToolArgumentParser
{
    public static T ParseRequired<T>(JsonElement? arguments, string toolName)
    {
        if (arguments is not { ValueKind: JsonValueKind.Object } argumentObject)
        {
            throw new ValidationException(
                "tool_arguments_invalid",
                $"Tool '{toolName}' requires an object 'arguments' payload.");
        }

        return Deserialize<T>(argumentObject, toolName);
    }

    public static T? ParseOptional<T>(JsonElement? arguments, string toolName) where T : class
    {
        if (arguments is null || arguments.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (arguments.Value.ValueKind is not JsonValueKind.Object)
        {
            throw new ValidationException(
                "tool_arguments_invalid",
                $"Tool '{toolName}' requires an object 'arguments' payload.");
        }

        return Deserialize<T>(arguments.Value, toolName);
    }

    private static T Deserialize<T>(JsonElement arguments, string toolName)
    {
        try
        {
            return arguments.Deserialize<T>(McpJsonOptions.Default)
                   ?? throw new ValidationException(
                       "tool_arguments_invalid",
                       $"Tool '{toolName}' arguments payload is invalid.");
        }
        catch (JsonException exception)
        {
            throw new ValidationException(
                "tool_arguments_invalid",
                $"Tool '{toolName}' arguments payload is invalid: {exception.Message}");
        }
    }
}

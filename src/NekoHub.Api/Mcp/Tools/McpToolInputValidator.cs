using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Mcp.Tools;

internal static class McpToolInputValidator
{
    public static Guid ParseRequiredGuid(string? value, string argumentName)
    {
        if (!Guid.TryParse(value, out var parsed))
        {
            throw new ValidationException(
                "tool_argument_invalid",
                $"Argument '{argumentName}' must be a valid GUID.");
        }

        return parsed;
    }

    public static Guid? ParseOptionalGuid(string? value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Guid.TryParse(value, out var parsed))
        {
            throw new ValidationException(
                "tool_argument_invalid",
                $"Argument '{argumentName}' must be a valid GUID.");
        }

        return parsed;
    }
}

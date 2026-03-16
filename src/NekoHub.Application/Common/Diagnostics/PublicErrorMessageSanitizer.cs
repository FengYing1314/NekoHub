using System.Text.RegularExpressions;

namespace NekoHub.Application.Common.Diagnostics;

public static partial class PublicErrorMessageSanitizer
{
    public static string Sanitize(Exception exception, int maxLength, string fallbackMessage)
    {
        return Sanitize(exception.Message, maxLength, fallbackMessage);
    }

    public static string Sanitize(string? message, int maxLength, string fallbackMessage)
    {
        var resolved = string.IsNullOrWhiteSpace(message)
            ? fallbackMessage
            : message.Trim();

        resolved = NormalizeWhitespaceRegex().Replace(resolved, " ");
        resolved = BearerTokenRegex().Replace(resolved, "Bearer ***");
        resolved = OpenAiLikeSecretRegex().Replace(resolved, "sk-***");

        if (resolved.Length > maxLength)
        {
            resolved = resolved[..maxLength];
        }

        return resolved;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex NormalizeWhitespaceRegex();

    [GeneratedRegex(@"Bearer\s+[^\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"sk-[A-Za-z0-9_-]+", RegexOptions.IgnoreCase)]
    private static partial Regex OpenAiLikeSecretRegex();
}

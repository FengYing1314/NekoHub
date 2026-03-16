using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NekoHub.Api.Auth;

internal static class PrincipalClaimReader
{
    public static Guid? GetUserId(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        return TryParseGuid(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub),
            principal.FindFirstValue("nameid"));
    }

    public static string? GetUsername(ClaimsPrincipal principal)
    {
        return FirstNonEmpty(
            principal.FindFirstValue(ClaimTypes.Name),
            principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName),
            principal.FindFirstValue("unique_name"));
    }

    public static string? GetRole(ClaimsPrincipal principal)
    {
        return FirstNonEmpty(
            principal.FindFirstValue(ClaimTypes.Role),
            principal.FindFirstValue("role"));
    }

    private static Guid? TryParseGuid(params string?[] values)
    {
        foreach (var value in values)
        {
            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
    }
}

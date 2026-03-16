using System.Security.Claims;
using NekoHub.Application.Auth;
using NekoHub.Domain.Users;

namespace NekoHub.Api.Auth;

public static class CurrentActorFactory
{
    public static CurrentActor Create(ClaimsPrincipal principal)
    {
        if (IsApiKeyPrincipal(principal))
        {
            return CurrentActor.MachineAdmin();
        }

        var userId = PrincipalClaimReader.GetUserId(principal);
        var username = PrincipalClaimReader.GetUsername(principal);
        UserRole? role = Enum.TryParse<UserRole>(PrincipalClaimReader.GetRole(principal), ignoreCase: true, out var parsedRole)
            ? parsedRole
            : null;

        return new CurrentActor(userId, username, role, false);
    }

    public static bool IsApiKeyPrincipal(ClaimsPrincipal principal)
    {
        var identity = principal.Identity;
        if (identity?.IsAuthenticated != true
            || !string.Equals(
                identity.AuthenticationType,
                ApiKeyAuthenticationDefaults.SchemeName,
                StringComparison.Ordinal))
        {
            return false;
        }

        var principalType = principal.FindFirstValue(ApiKeyAuthenticationDefaults.PrincipalTypeClaimType);
        var fingerprint = principal.FindFirstValue(ApiKeyAuthenticationDefaults.FingerprintClaimType);
        return string.Equals(
                   principalType,
                   ApiKeyAuthenticationDefaults.ApiKeyPrincipalType,
                   StringComparison.Ordinal)
               && IsValidFingerprint(fingerprint);
    }

    private static bool IsValidFingerprint(string? fingerprint)
    {
        return !string.IsNullOrWhiteSpace(fingerprint)
            && fingerprint.Length == 16
            && fingerprint.All(Uri.IsHexDigit);
    }
}

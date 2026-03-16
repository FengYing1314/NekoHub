namespace NekoHub.Api.Auth;

public static class AuthorizationPolicies
{
    public const string JwtUserRequired = "JwtUserRequired";
    public const string ManagementAccess = "ManagementAccess";
    public const string ApiKeyOnly = "ApiKeyOnly";
    public const string HybridBearer = "HybridBearer";
}

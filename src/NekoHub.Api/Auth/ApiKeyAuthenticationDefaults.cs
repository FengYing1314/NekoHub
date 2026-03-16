namespace NekoHub.Api.Auth;

public static class ApiKeyAuthenticationDefaults
{
    public const string SchemeName = "ApiKey";
    public const string FingerprintClaimType = "nekohub:auth:api_key_fingerprint";
    public const string PrincipalTypeClaimType = "nekohub:auth:principal_type";
    public const string ApiKeyPrincipalType = "api_key";
}

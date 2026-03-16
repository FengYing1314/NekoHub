namespace NekoHub.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Auth:Jwt";

    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "NekoHub";

    public string Audience { get; set; } = "NekoHub.Web";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;
}

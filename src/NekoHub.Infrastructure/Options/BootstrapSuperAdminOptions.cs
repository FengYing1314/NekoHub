namespace NekoHub.Infrastructure.Options;

public sealed class BootstrapSuperAdminOptions
{
    public const string SectionName = "Auth:BootstrapSuperAdmin";

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

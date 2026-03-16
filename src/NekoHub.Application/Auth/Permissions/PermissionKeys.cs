namespace NekoHub.Application.Auth.Permissions;

public static class PermissionKeys
{
    public const string AssetsRead = "assets.read";
    public const string AssetsCreate = "assets.create";
    public const string AssetsUpdate = "assets.update";
    public const string AssetsDelete = "assets.delete";

    public const string ProvidersRead = "providers.read";
    public const string ProvidersCreate = "providers.create";
    public const string ProvidersUpdate = "providers.update";
    public const string ProvidersDelete = "providers.delete";

    public const string AiProvidersRead = "aiProviders.read";
    public const string AiProvidersCreate = "aiProviders.create";
    public const string AiProvidersUpdate = "aiProviders.update";
    public const string AiProvidersDelete = "aiProviders.delete";

    public const string SettingsRead = "settings.read";
    public const string SettingsUpdate = "settings.update";

    public const string UsersRead = "users.read";
    public const string UsersCreate = "users.create";
    public const string UsersUpdate = "users.update";
    public const string UsersDisable = "users.disable";
    public const string UsersManagePermissions = "users.managePermissions";
}

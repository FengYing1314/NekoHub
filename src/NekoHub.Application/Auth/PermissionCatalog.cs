using NekoHub.Domain.Users;

namespace NekoHub.Application.Auth;

public static class PermissionCatalog
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

    public static readonly IReadOnlyList<string> All =
    [
        AssetsRead,
        AssetsCreate,
        AssetsUpdate,
        AssetsDelete,
        ProvidersRead,
        ProvidersCreate,
        ProvidersUpdate,
        ProvidersDelete,
        AiProvidersRead,
        AiProvidersCreate,
        AiProvidersUpdate,
        AiProvidersDelete,
        SettingsRead,
        SettingsUpdate,
        UsersRead,
        UsersCreate,
        UsersUpdate,
        UsersDisable,
        UsersManagePermissions
    ];

    public static IReadOnlyList<string> GetDefaultPermissions(UserRole role)
    {
        return role switch
        {
            UserRole.SuperAdmin => All,
            UserRole.Admin =>
            [
                AssetsRead,
                AssetsCreate,
                AssetsUpdate,
                AssetsDelete,
                ProvidersRead,
                ProvidersCreate,
                ProvidersUpdate,
                ProvidersDelete,
                AiProvidersRead,
                AiProvidersCreate,
                AiProvidersUpdate,
                AiProvidersDelete,
                SettingsRead,
                SettingsUpdate,
                UsersRead,
                UsersCreate,
                UsersUpdate,
                UsersDisable,
                UsersManagePermissions
            ],
            _ =>
            [
                AssetsRead
            ]
        };
    }
}

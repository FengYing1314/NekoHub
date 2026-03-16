using NekoHub.Domain.Users;

namespace NekoHub.Application.Auth.Permissions;

public static class RolePermissionTemplates
{
    private static readonly IReadOnlyList<string> AdminPermissions =
    [
        PermissionKeys.AssetsRead,
        PermissionKeys.AssetsCreate,
        PermissionKeys.AssetsUpdate,
        PermissionKeys.AssetsDelete,
        PermissionKeys.ProvidersRead,
        PermissionKeys.ProvidersCreate,
        PermissionKeys.ProvidersUpdate,
        PermissionKeys.ProvidersDelete,
        PermissionKeys.AiProvidersRead,
        PermissionKeys.AiProvidersCreate,
        PermissionKeys.AiProvidersUpdate,
        PermissionKeys.AiProvidersDelete,
        PermissionKeys.SettingsRead,
        PermissionKeys.SettingsUpdate,
        PermissionKeys.UsersRead,
        PermissionKeys.UsersCreate,
        PermissionKeys.UsersUpdate,
        PermissionKeys.UsersDisable,
        PermissionKeys.UsersManagePermissions
    ];

    private static readonly IReadOnlyList<string> UserPermissions =
    [
        PermissionKeys.AssetsRead
    ];

    public static IReadOnlyList<string> GetDefaultPermissions(UserRole role)
    {
        return role switch
        {
            UserRole.SuperAdmin => PermissionCatalog.All,
            UserRole.Admin => AdminPermissions,
            _ => UserPermissions
        };
    }
}

namespace NekoHub.Application.Auth.Permissions;

public static class PermissionCatalog
{
    public static readonly IReadOnlyList<string> All =
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

    private static readonly HashSet<string> KnownPermissions = new(All, StringComparer.Ordinal);

    public static bool IsKnown(string permission)
    {
        return KnownPermissions.Contains(permission);
    }

    public static IReadOnlyList<string> NormalizePermissions(IEnumerable<string>? permissions)
    {
        if (permissions is null)
        {
            return [];
        }

        return permissions
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Select(static permission => permission.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static permission => permission, StringComparer.Ordinal)
            .ToList();
    }
}

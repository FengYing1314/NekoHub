using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Domain.Users;

namespace NekoHub.Application.Auth.Services;

public sealed class PermissionService(IUserPermissionGrantRepository userPermissionGrantRepository) : IPermissionService
{
    public async Task<IReadOnlyList<string>> GetPermissionsAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        if (user.Role == UserRole.SuperAdmin)
        {
            return PermissionCatalog.All;
        }

        var grants = await userPermissionGrantRepository.ListByUserIdAsync(user.Id, cancellationToken);
        return grants
            .Select(static grant => grant.Permission)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        UserRole role,
        string permission,
        CancellationToken cancellationToken = default)
    {
        if (role == UserRole.SuperAdmin)
        {
            return true;
        }

        var normalizedPermission = NormalizePermission(permission);
        var grants = await userPermissionGrantRepository.ListByUserIdAsync(userId, cancellationToken);
        return grants.Any(grant => string.Equals(grant.Permission, normalizedPermission, StringComparison.Ordinal));
    }

    public IReadOnlyList<string> NormalizePermissions(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var normalized = permissions
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Select(NormalizePermission)
            .Where(static permission => PermissionCatalog.All.Contains(permission, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return normalized;
    }

    private static string NormalizePermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            throw new ArgumentException("Permission is required.", nameof(permission));
        }

        return permission.Trim();
    }
}

using NekoHub.Application.Auth.Dtos;

using NekoHub.Domain.Users;

namespace NekoHub.Application.Auth.Services;

public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetPermissionsAsync(User user, CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(
        Guid userId,
        UserRole role,
        string permission,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> NormalizePermissions(IEnumerable<string> permissions);
}

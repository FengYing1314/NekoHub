using NekoHub.Domain.Users;

namespace NekoHub.Application.Abstractions.Persistence;

public interface IUserPermissionGrantRepository
{
    Task<IReadOnlyList<UserPermissionGrant>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task ReplaceForUserAsync(
        Guid userId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default);
}

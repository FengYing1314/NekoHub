using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Domain.Users;

namespace NekoHub.Infrastructure.Persistence.EfCore;

public sealed class EfCoreUserPermissionGrantRepository(AssetDbContext dbContext) : IUserPermissionGrantRepository
{
    public async Task<IReadOnlyList<UserPermissionGrant>> ListByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.UserPermissionGrants
            .AsNoTracking()
            .Where(grant => grant.UserId == userId)
            .OrderBy(grant => grant.Permission)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceForUserAsync(
        Guid userId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var existingGrants = await dbContext.UserPermissionGrants
            .Where(grant => grant.UserId == userId)
            .ToListAsync(cancellationToken);

        if (existingGrants.Count > 0)
        {
            dbContext.UserPermissionGrants.RemoveRange(existingGrants);
        }

        var nextGrants = permissions
            .Distinct(StringComparer.Ordinal)
            .Select(permission => new UserPermissionGrant(userId, permission))
            .ToList();

        if (nextGrants.Count > 0)
        {
            await dbContext.UserPermissionGrants.AddRangeAsync(nextGrants, cancellationToken);
        }
    }
}

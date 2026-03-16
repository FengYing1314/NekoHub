using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Domain.Users;

namespace NekoHub.Infrastructure.Persistence.EfCore;

public sealed class EfCoreUserRepository(AssetDbContext dbContext) : IUserRepository
{
    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AddAsync(user, cancellationToken).AsTask();
    }

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = NormalizeUsername(username);
        return dbContext.Users.SingleOrDefaultAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .AsNoTracking()
            .OrderByDescending(user => user.CreatedAtUtc)
            .ThenBy(user => user.Username)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AnyByUsernameAsync(
        string username,
        Guid? excludedUserId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = NormalizeUsername(username);
        return dbContext.Users.AnyAsync(
            user => user.NormalizedUsername == normalizedUsername && (!excludedUserId.HasValue || user.Id != excludedUserId.Value),
            cancellationToken);
    }

    public Task<bool> AnySuperAdminAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AnyAsync(user => user.Role == UserRole.SuperAdmin, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeUsername(string username)
    {
        return string.IsNullOrWhiteSpace(username)
            ? string.Empty
            : username.Trim().ToUpperInvariant();
    }
}

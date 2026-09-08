using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Domain.Users;

namespace NekoHub.Infrastructure.Persistence.EfCore;

public sealed class EfCoreRefreshTokenRepository(AssetDbContext dbContext) : IRefreshTokenRepository
{
    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        return dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken).AsTask();
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RefreshTokens
            .Where(token => token.UserId == userId)
            .OrderByDescending(token => token.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryIssueAsync(User user, RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var currentUser = await LockUserAsync(user.Id, cancellationToken);
        if (currentUser is null || !currentUser.IsActive || currentUser.PasswordHash != user.PasswordHash)
        {
            return false;
        }

        await dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryRotateAsync(
        RefreshToken currentToken,
        RefreshToken nextToken,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(currentToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return false;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var consumed = await dbContext.RefreshTokens
            .Where(token => token.Id == currentToken.Id
                && token.UserId == nextToken.UserId
                && token.RevokedAtUtc == null
                && token.ExpiresAtUtc > nowUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAtUtc, nowUtc)
                .SetProperty(token => token.ReplacedByTokenId, nextToken.Id), cancellationToken);
        if (consumed == 0)
        {
            return false;
        }

        await dbContext.RefreshTokens.AddAsync(nextToken, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RevokeSessionAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(refreshToken.UserId, cancellationToken);
        var current = await GetSessionTipAsync(refreshToken.UserId, refreshToken.Id, cancellationToken);
        if (current is not null)
        {
            await dbContext.RefreshTokens
                .Where(token => token.Id == current.Id && token.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(token => token.RevokedAtUtc, DateTimeOffset.UtcNow), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeAllAndSaveChangesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // 与签发、轮换使用同一用户行锁，确保重置密码不会漏掉刚生成的后继令牌。
        await LockUserAsync(userId, cancellationToken);
        await dbContext.RefreshTokens
            .Where(token => token.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAtUtc, DateTimeOffset.UtcNow), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> IsSessionActiveAsync(Guid userId, string jwtId, CancellationToken cancellationToken = default)
    {
        var token = await dbContext.RefreshTokens.AsNoTracking()
            .SingleOrDefaultAsync(token => token.UserId == userId && token.JwtId == jwtId, cancellationToken);
        if (token is null)
        {
            return false;
        }

        // 正常轮换不提前废弃仍在有效期内的 access token；登出和密码重置则撤销整个会话链。
        var current = token.ReplacedByTokenId is { } nextTokenId
            ? await GetSessionTipAsync(userId, nextTokenId, cancellationToken)
            : token;
        return current?.IsActive(DateTimeOffset.UtcNow) == true;
    }

    private Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<RefreshToken?> GetSessionTipAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        while (visited.Add(tokenId))
        {
            var token = await dbContext.RefreshTokens.AsNoTracking()
                .SingleOrDefaultAsync(token => token.Id == tokenId && token.UserId == userId, cancellationToken);
            if (token?.ReplacedByTokenId is not { } nextTokenId)
            {
                return token;
            }

            tokenId = nextTokenId;
        }

        return null;
    }
}

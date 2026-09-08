using NekoHub.Domain.Users;

namespace NekoHub.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> TryIssueAsync(User user, RefreshToken refreshToken, CancellationToken cancellationToken = default);

    Task<bool> TryRotateAsync(RefreshToken currentToken, RefreshToken nextToken, CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAllAndSaveChangesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsSessionActiveAsync(Guid userId, string jwtId, CancellationToken cancellationToken = default);
}

using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Security;
using NekoHub.Application.Auth.Dtos;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Domain.Users;

namespace NekoHub.Application.Auth.Services;

public sealed class AuthService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHashService passwordHashService,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IPermissionService permissionService) : IAuthService
{
    public async Task<AuthSessionDto> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByUsernameAsync(username, cancellationToken);
        if (user is null || !passwordHashService.VerifyPassword(user, password))
        {
            throw new UnauthorizedException("auth_invalid_credentials", "Invalid username or password.");
        }

        EnsureActiveUser(user);

        var permissions = await permissionService.GetPermissionsAsync(user, cancellationToken);
        var accessToken = jwtTokenService.CreateAccessToken(user, permissions);
        var issuedRefreshToken = refreshTokenService.IssueRefreshToken(user, accessToken.JwtId);

        user.RecordLogin();
        await refreshTokenRepository.AddAsync(issuedRefreshToken.Entity, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return new AuthSessionDto(
            AccessToken: accessToken.AccessToken,
            RefreshToken: issuedRefreshToken.RefreshToken,
            AccessTokenExpiresAtUtc: accessToken.ExpiresAtUtc,
            RefreshTokenExpiresAtUtc: issuedRefreshToken.Entity.ExpiresAtUtc,
            User: ToAuthenticatedUserDto(user, permissions));
    }

    public async Task<AuthSessionDto> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var normalizedRefreshToken = NormalizeRequired(refreshToken, nameof(refreshToken));
        var tokenHash = refreshTokenService.ComputeHash(normalizedRefreshToken);
        var storedToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (storedToken is null)
        {
            throw new UnauthorizedException("auth_refresh_token_invalid", "Refresh token is invalid.");
        }

        var user = await userRepository.GetByIdAsync(storedToken.UserId, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedException("auth_refresh_token_invalid", "Refresh token is invalid.");
        }

        EnsureActiveUser(user);

        var nowUtc = DateTimeOffset.UtcNow;
        if (storedToken.IsRevoked)
        {
            await RevokeActiveTokensAsync(user.Id, cancellationToken);
            throw new UnauthorizedException("auth_refresh_token_reused", "Refresh token has already been used.");
        }

        if (storedToken.IsExpired(nowUtc))
        {
            storedToken.Revoke();
            await userRepository.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("auth_refresh_token_expired", "Refresh token has expired.");
        }

        var permissions = await permissionService.GetPermissionsAsync(user, cancellationToken);
        var accessToken = jwtTokenService.CreateAccessToken(user, permissions);
        var nextRefreshToken = refreshTokenService.IssueRefreshToken(user, accessToken.JwtId);

        storedToken.Revoke(nextRefreshToken.Entity.Id);
        await refreshTokenRepository.AddAsync(nextRefreshToken.Entity, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return new AuthSessionDto(
            AccessToken: accessToken.AccessToken,
            RefreshToken: nextRefreshToken.RefreshToken,
            AccessTokenExpiresAtUtc: accessToken.ExpiresAtUtc,
            RefreshTokenExpiresAtUtc: nextRefreshToken.Entity.ExpiresAtUtc,
            User: ToAuthenticatedUserDto(user, permissions));
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = refreshTokenService.ComputeHash(refreshToken);
        var storedToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (storedToken is null || storedToken.IsRevoked)
        {
            return;
        }

        storedToken.Revoke();
        await userRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthenticatedUserDto> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedException("auth_user_not_found", "Authenticated user was not found.");

        EnsureActiveUser(user);
        var permissions = await permissionService.GetPermissionsAsync(user, cancellationToken);
        return ToAuthenticatedUserDto(user, permissions);
    }

    private async Task RevokeActiveTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await refreshTokenRepository.ListByUserIdAsync(userId, cancellationToken);
        foreach (var token in tokens.Where(static token => !token.IsRevoked))
        {
            token.Revoke();
        }

        await userRepository.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureActiveUser(User user)
    {
        if (!user.IsActive)
        {
            throw new UnauthorizedException("auth_user_inactive", "User account is inactive.");
        }
    }

    private static AuthenticatedUserDto ToAuthenticatedUserDto(User user, IReadOnlyList<string> permissions)
    {
        return new AuthenticatedUserDto(
            Id: user.Id,
            Username: user.Username,
            Role: user.Role,
            IsActive: user.IsActive,
            CreatedAtUtc: user.CreatedAtUtc,
            UpdatedAtUtc: user.UpdatedAtUtc,
            LastLoginAtUtc: user.LastLoginAtUtc,
            Permissions: permissions);
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("auth_request_invalid", $"{parameterName} is required.");
        }

        return value.Trim();
    }
}

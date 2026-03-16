using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NekoHub.Api.Configuration;
using NekoHub.Application.Abstractions.Security;
using NekoHub.Domain.Users;

namespace NekoHub.Api.Auth;

public sealed class RefreshTokenService(IOptions<JwtOptions> options) : IRefreshTokenService
{
    private readonly JwtOptions _options = options.Value;

    public IssuedRefreshToken IssueRefreshToken(User user, string jwtId)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = ComputeHash(rawToken);
        var expiresAtUtc = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays);
        var entity = new RefreshToken(
            id: Guid.CreateVersion7(),
            userId: user.Id,
            tokenHash: tokenHash,
            jwtId: jwtId,
            expiresAtUtc: expiresAtUtc);

        return new IssuedRefreshToken(rawToken, entity);
    }

    public string ComputeHash(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return string.Empty;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken.Trim()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

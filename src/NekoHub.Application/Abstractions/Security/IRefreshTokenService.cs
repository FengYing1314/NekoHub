using NekoHub.Domain.Users;

namespace NekoHub.Application.Abstractions.Security;

public interface IRefreshTokenService
{
    IssuedRefreshToken IssueRefreshToken(User user, string jwtId);

    string ComputeHash(string refreshToken);
}

public sealed record IssuedRefreshToken(
    string RefreshToken,
    RefreshToken Entity);

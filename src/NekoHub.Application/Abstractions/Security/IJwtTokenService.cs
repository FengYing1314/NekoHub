using NekoHub.Application.Auth.Dtos;
using NekoHub.Domain.Users;

namespace NekoHub.Application.Abstractions.Security;

public interface IJwtTokenService
{
    IssuedAccessTokenDto CreateAccessToken(User user, IReadOnlyCollection<string> permissions);
}

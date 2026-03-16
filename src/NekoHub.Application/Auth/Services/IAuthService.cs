using NekoHub.Application.Auth.Dtos;

namespace NekoHub.Application.Auth.Services;

public interface IAuthService
{
    Task<AuthSessionDto> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    Task<AuthSessionDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<AuthenticatedUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

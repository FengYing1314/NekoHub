namespace NekoHub.Application.Auth.Dtos;

public sealed record AuthSessionDto(
    AuthenticatedUserDto User,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

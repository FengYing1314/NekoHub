namespace NekoHub.Application.Auth.Dtos;

public sealed record IssuedAccessTokenDto(
    string AccessToken,
    string JwtId,
    DateTimeOffset ExpiresAtUtc);

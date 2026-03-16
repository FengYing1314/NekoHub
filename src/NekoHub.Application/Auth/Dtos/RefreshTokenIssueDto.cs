using NekoHub.Domain.Users;

namespace NekoHub.Application.Auth.Dtos;

public sealed record RefreshTokenIssueDto(
    User User,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc);

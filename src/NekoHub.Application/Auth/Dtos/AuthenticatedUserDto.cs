using NekoHub.Domain.Users;

namespace NekoHub.Application.Auth.Dtos;

public sealed record AuthenticatedUserDto(
    Guid Id,
    string Username,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastLoginAtUtc,
    IReadOnlyList<string> Permissions);

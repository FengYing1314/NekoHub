using NekoHub.Domain.Users;

namespace NekoHub.Application.Users.Dtos;

public sealed record UserListItemDto(
    Guid Id,
    string Username,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastLoginAtUtc,
    IReadOnlyList<string> Permissions);

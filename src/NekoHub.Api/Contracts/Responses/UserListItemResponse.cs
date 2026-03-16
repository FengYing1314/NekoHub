using NekoHub.Domain.Users;

namespace NekoHub.Api.Contracts.Responses;

public sealed record UserListItemResponse(
    Guid Id,
    string Username,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastLoginAtUtc,
    IReadOnlyList<string> Permissions);

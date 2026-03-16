using NekoHub.Domain.Users;

namespace NekoHub.Api.Contracts.Requests;

public sealed record CreateUserRequest(
    string Username,
    string Password,
    UserRole Role,
    bool? IsActive,
    IReadOnlyList<string>? Permissions);

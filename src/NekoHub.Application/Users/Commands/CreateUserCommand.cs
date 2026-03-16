using NekoHub.Domain.Users;

namespace NekoHub.Application.Users.Commands;

public sealed record CreateUserCommand(
    string Username,
    string Password,
    UserRole Role,
    bool IsActive,
    IReadOnlyList<string>? Permissions);

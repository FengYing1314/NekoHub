using NekoHub.Domain.Users;

namespace NekoHub.Application.Users.Dtos;

public sealed record CreateUserInput(
    string Username,
    string Password,
    UserRole Role,
    bool IsActive,
    IReadOnlyList<string>? Permissions);

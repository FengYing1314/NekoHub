using NekoHub.Domain.Users;

namespace NekoHub.Application.Users.Dtos;

public sealed record UpdateUserInput(
    string Username,
    UserRole Role,
    bool IsActive);

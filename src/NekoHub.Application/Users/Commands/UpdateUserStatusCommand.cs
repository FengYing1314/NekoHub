namespace NekoHub.Application.Users.Commands;

public sealed record UpdateUserStatusCommand(
    Guid UserId,
    bool IsActive);

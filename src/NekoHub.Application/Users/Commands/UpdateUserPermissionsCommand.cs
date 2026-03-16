namespace NekoHub.Application.Users.Commands;

public sealed record UpdateUserPermissionsCommand(Guid UserId, IReadOnlyList<string> Permissions);

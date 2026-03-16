namespace NekoHub.Application.Users.Dtos;

public sealed record UpdateUserPermissionsInput(IReadOnlyList<string> Permissions);

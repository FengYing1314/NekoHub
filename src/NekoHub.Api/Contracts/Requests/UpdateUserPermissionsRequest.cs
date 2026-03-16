namespace NekoHub.Api.Contracts.Requests;

public sealed record UpdateUserPermissionsRequest(IReadOnlyList<string> Permissions);

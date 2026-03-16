using NekoHub.Domain.Users;

namespace NekoHub.Application.Auth;

public sealed record CurrentActor(
    Guid? UserId,
    string? Username,
    UserRole? Role,
    bool IsApiKey)
{
    public static CurrentActor MachineAdmin() => new(null, "api-key", null, true);
}

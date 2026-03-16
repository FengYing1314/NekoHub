using NekoHub.Domain.Users;

namespace NekoHub.Application.Auth.Dtos;

public sealed record AuthenticatedActor(
    bool IsMachine,
    Guid? UserId,
    UserRole? Role);

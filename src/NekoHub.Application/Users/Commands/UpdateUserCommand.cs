using NekoHub.Application.Common.Models;
using NekoHub.Domain.Users;

namespace NekoHub.Application.Users.Commands;

public sealed record UpdateUserCommand(
    Guid UserId,
    OptionalValue<string?> Username,
    OptionalValue<UserRole> Role,
    OptionalValue<bool> IsActive);

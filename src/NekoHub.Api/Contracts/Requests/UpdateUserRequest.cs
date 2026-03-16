using NekoHub.Application.Common.Models;
using NekoHub.Domain.Users;

namespace NekoHub.Api.Contracts.Requests;

public sealed class UpdateUserRequest
{
    public OptionalValue<string?> Username { get; init; }

    public OptionalValue<UserRole> Role { get; init; }

    public OptionalValue<bool> IsActive { get; init; }
}

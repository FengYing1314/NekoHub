using NekoHub.Application.Auth;
using NekoHub.Application.Users.Commands;
using NekoHub.Application.Users.Dtos;

namespace NekoHub.Application.Users.Services;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserListItemDto>> ListAsync(
        CurrentActor actor,
        CancellationToken cancellationToken = default);

    Task<UserDetailDto> GetByIdAsync(
        CurrentActor actor,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserDetailDto> CreateAsync(
        CurrentActor actor,
        CreateUserCommand command,
        CancellationToken cancellationToken = default);

    Task<UserDetailDto> UpdateAsync(
        CurrentActor actor,
        UpdateUserCommand command,
        CancellationToken cancellationToken = default);

    Task<UserDetailDto> SetActiveAsync(
        CurrentActor actor,
        UpdateUserStatusCommand command,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        CurrentActor actor,
        ResetUserPasswordCommand command,
        CancellationToken cancellationToken = default);

    Task<UserDetailDto> UpdatePermissionsAsync(
        CurrentActor actor,
        UpdateUserPermissionsCommand command,
        CancellationToken cancellationToken = default);
}

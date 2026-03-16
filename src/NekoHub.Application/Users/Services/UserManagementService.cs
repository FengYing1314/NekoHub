using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Security;
using NekoHub.Application.Auth;
using NekoHub.Application.Auth.Services;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Application.Users.Commands;
using NekoHub.Application.Users.Dtos;
using NekoHub.Domain.Users;

namespace NekoHub.Application.Users.Services;

public sealed class UserManagementService(
    IUserRepository userRepository,
    IUserPermissionGrantRepository userPermissionGrantRepository,
    IPasswordHashService passwordHashService,
    IPermissionService permissionService) : IUserManagementService
{
    public async Task<IReadOnlyList<UserListItemDto>> ListAsync(
        CurrentActor actor,
        CancellationToken cancellationToken = default)
    {
        var users = await userRepository.ListAsync(cancellationToken);
        var result = new List<UserListItemDto>();
        foreach (var user in users
                     .Where(user => CanView(actor, user))
                     .OrderByDescending(static user => user.CreatedAtUtc)
                     .ThenBy(static user => user.Username, StringComparer.Ordinal))
        {
            var permissions = await permissionService.GetPermissionsAsync(user, cancellationToken);

            result.Add(new UserListItemDto(
                Id: user.Id,
                Username: user.Username,
                Role: user.Role,
                IsActive: user.IsActive,
                CreatedAtUtc: user.CreatedAtUtc,
                UpdatedAtUtc: user.UpdatedAtUtc,
                LastLoginAtUtc: user.LastLoginAtUtc,
                Permissions: permissions));
        }

        return result;
    }

    public async Task<UserDetailDto> GetByIdAsync(
        CurrentActor actor,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("user_not_found", $"User '{userId}' was not found.");
        EnsureCanManageTarget(actor, user);

        return await ToDetailDtoAsync(user, cancellationToken);
    }

    public async Task<UserDetailDto> CreateAsync(
        CurrentActor actor,
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureCanCreateRole(actor, command.Role);

        var normalizedUsername = NormalizeUsername(command.Username);
        if (await userRepository.AnyByUsernameAsync(normalizedUsername, cancellationToken: cancellationToken))
        {
            throw new ConflictException("user_username_conflict", $"Username '{normalizedUsername}' already exists.");
        }

        ValidatePassword(command.Password);

        var user = new User(
            id: Guid.CreateVersion7(),
            username: normalizedUsername,
            role: command.Role,
            passwordHash: "pending",
            isActive: command.IsActive);
        user.SetPasswordHash(passwordHashService.HashPassword(user, command.Password));

        var initialPermissions = command.Permissions is { Count: > 0 }
            ? permissionService.NormalizePermissions(command.Permissions)
            : PermissionCatalog.GetDefaultPermissions(command.Role);

        await userRepository.AddAsync(user, cancellationToken);
        await userPermissionGrantRepository.ReplaceForUserAsync(
            user.Id,
            initialPermissions,
            cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return await ToDetailDtoAsync(user, cancellationToken);
    }

    public async Task<UserDetailDto> UpdateAsync(
        CurrentActor actor,
        UpdateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("user_not_found", $"User '{command.UserId}' was not found.");

        EnsureCanManageTarget(actor, user);
        var nextRole = command.Role.IsSet ? command.Role.Value : user.Role;
        EnsureRoleTransitionAllowed(actor, user, nextRole);

        var normalizedUsername = command.Username.IsSet
            ? NormalizeUsername(command.Username.Value ?? user.Username)
            : user.Username;
        if (await userRepository.AnyByUsernameAsync(normalizedUsername, command.UserId, cancellationToken))
        {
            throw new ConflictException("user_username_conflict", $"Username '{normalizedUsername}' already exists.");
        }

        user.Rename(normalizedUsername);

        if (user.Role != nextRole)
        {
            user.SetRole(nextRole);
            await userPermissionGrantRepository.ReplaceForUserAsync(
                user.Id,
                PermissionCatalog.GetDefaultPermissions(nextRole),
                cancellationToken);
        }

        if (command.IsActive.IsSet)
        {
            user.SetActive(command.IsActive.Value);
        }

        await userRepository.SaveChangesAsync(cancellationToken);
        return await ToDetailDtoAsync(user, cancellationToken);
    }

    public async Task<UserDetailDto> SetActiveAsync(
        CurrentActor actor,
        UpdateUserStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("user_not_found", $"User '{command.UserId}' was not found.");

        EnsureCanManageTarget(actor, user);

        if (user.Role == UserRole.SuperAdmin && !command.IsActive)
        {
            throw new ForbiddenException("user_super_admin_disable_forbidden", "Super admin account cannot be disabled.");
        }

        if (!actor.IsApiKey && actor.UserId == user.Id && !command.IsActive)
        {
            throw new ForbiddenException("user_self_disable_forbidden", "You cannot disable your own account.");
        }

        user.SetActive(command.IsActive);
        await userRepository.SaveChangesAsync(cancellationToken);
        return await ToDetailDtoAsync(user, cancellationToken);
    }

    public async Task ResetPasswordAsync(
        CurrentActor actor,
        ResetUserPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("user_not_found", $"User '{command.UserId}' was not found.");

        EnsureCanManageTarget(actor, user);
        ValidatePassword(command.NewPassword);

        user.SetPasswordHash(passwordHashService.HashPassword(user, command.NewPassword));
        await userRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDetailDto> UpdatePermissionsAsync(
        CurrentActor actor,
        UpdateUserPermissionsCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("user_not_found", $"User '{command.UserId}' was not found.");

        EnsureCanManageTarget(actor, user);

        if (user.Role == UserRole.SuperAdmin)
        {
            throw new ForbiddenException(
                "user_super_admin_permissions_forbidden",
                "Super admin permissions are fixed and cannot be changed.");
        }

        if (!actor.IsApiKey && actor.UserId == user.Id)
        {
            throw new ForbiddenException(
                "user_self_permissions_forbidden",
                "You cannot edit your own permissions.");
        }

        var normalizedPermissions = permissionService.NormalizePermissions(command.Permissions);
        await userPermissionGrantRepository.ReplaceForUserAsync(user.Id, normalizedPermissions, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);
        return await ToDetailDtoAsync(user, cancellationToken);
    }

    private async Task<UserDetailDto> ToDetailDtoAsync(User user, CancellationToken cancellationToken)
    {
        var permissions = await permissionService.GetPermissionsAsync(user, cancellationToken);
        return new UserDetailDto(
            Id: user.Id,
            Username: user.Username,
            Role: user.Role,
            IsActive: user.IsActive,
            CreatedAtUtc: user.CreatedAtUtc,
            UpdatedAtUtc: user.UpdatedAtUtc,
            LastLoginAtUtc: user.LastLoginAtUtc,
            Permissions: permissions);
    }

    private static bool CanView(CurrentActor actor, User user)
    {
        if (actor.IsApiKey || actor.Role == UserRole.SuperAdmin)
        {
            return true;
        }

        return actor.Role == UserRole.Admin && user.Role == UserRole.User;
    }

    private static void EnsureCanCreateRole(CurrentActor actor, UserRole role)
    {
        if (role == UserRole.SuperAdmin)
        {
            throw new ForbiddenException(
                "user_super_admin_create_forbidden",
                "Super admin can only be created by bootstrap.");
        }

        if (actor.IsApiKey)
        {
            return;
        }

        if (actor.Role == UserRole.SuperAdmin)
        {
            return;
        }

        if (actor.Role == UserRole.Admin && role == UserRole.User)
        {
            return;
        }

        throw new ForbiddenException("user_create_forbidden", "You do not have permission to create this user.");
    }

    private static void EnsureCanManageTarget(CurrentActor actor, User target)
    {
        if (actor.IsApiKey)
        {
            return;
        }

        if (actor.Role == UserRole.SuperAdmin)
        {
            if (target.Role == UserRole.SuperAdmin && actor.UserId != target.Id)
            {
                throw new ForbiddenException(
                    "user_manage_super_admin_forbidden",
                    "Super admin account can only manage itself.");
            }

            return;
        }

        if (actor.Role == UserRole.Admin && target.Role == UserRole.User)
        {
            return;
        }

        throw new ForbiddenException("user_manage_forbidden", "You do not have permission to manage this user.");
    }

    private static void EnsureRoleTransitionAllowed(CurrentActor actor, User target, UserRole nextRole)
    {
        if (nextRole == UserRole.SuperAdmin && target.Role != UserRole.SuperAdmin)
        {
            throw new ForbiddenException(
                "user_super_admin_promote_forbidden",
                "Super admin role can only be created by bootstrap.");
        }

        if (target.Role == UserRole.SuperAdmin && nextRole != UserRole.SuperAdmin)
        {
            throw new ForbiddenException(
                "user_super_admin_role_change_forbidden",
                "Super admin role cannot be changed.");
        }

        if (!actor.IsApiKey && actor.UserId == target.Id && target.Role != nextRole)
        {
            throw new ForbiddenException(
                "user_self_role_change_forbidden",
                "You cannot change your own role.");
        }
    }

    private static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ValidationException("user_username_required", "Username is required.");
        }

        return username.Trim().ToLowerInvariant();
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ValidationException("user_password_required", "Password is required.");
        }
    }
}

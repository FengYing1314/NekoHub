using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NekoHub.Api.Auth;
using NekoHub.Api.Contracts.Requests;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Auth;
using NekoHub.Application.Users.Commands;
using NekoHub.Application.Users.Dtos;
using NekoHub.Application.Users.Services;

namespace NekoHub.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = AuthorizationPolicies.ManagementAccess)]
public sealed class UsersController(IUserManagementService userManagementService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCatalog.UsersRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var users = await userManagementService.ListAsync(CurrentActorFactory.Create(User), cancellationToken);
        var response = users.Select(ToListItemResponse).ToList();
        return Ok(ApiResponseFactory.Success<IReadOnlyList<UserListItemResponse>>(response));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionCatalog.UsersRead)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var user = await userManagementService.GetByIdAsync(CurrentActorFactory.Create(User), id, cancellationToken);
        return Ok(ApiResponseFactory.Success(ToDetailResponse(user)));
    }

    [HttpPost]
    [Authorize(Policy = PermissionCatalog.UsersCreate)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var created = await userManagementService.CreateAsync(
            CurrentActorFactory.Create(User),
            new CreateUserCommand(
                request.Username,
                request.Password,
                request.Role,
                request.IsActive ?? true,
                request.Permissions),
            cancellationToken);

        return Created($"/api/v1/users/{created.Id}", ApiResponseFactory.Success(ToDetailResponse(created)));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = PermissionCatalog.UsersUpdate)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await userManagementService.UpdateAsync(
            CurrentActorFactory.Create(User),
            new UpdateUserCommand(id, request.Username, request.Role, request.IsActive),
            cancellationToken);

        return Ok(ApiResponseFactory.Success(ToDetailResponse(updated)));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = PermissionCatalog.UsersDisable)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatusAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await userManagementService.SetActiveAsync(
            CurrentActorFactory.Create(User),
            new UpdateUserStatusCommand(id, request.IsActive),
            cancellationToken);

        return Ok(ApiResponseFactory.Success(ToDetailResponse(updated)));
    }

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = PermissionCatalog.UsersUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPasswordAsync(
        [FromRoute] Guid id,
        [FromBody] ResetUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await userManagementService.ResetPasswordAsync(
            CurrentActorFactory.Create(User),
            new ResetUserPasswordCommand(id, request.NewPassword),
            cancellationToken);

        return NoContent();
    }

    [HttpPatch("{id:guid}/permissions")]
    [Authorize(Policy = PermissionCatalog.UsersManagePermissions)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePermissionsAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateUserPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await userManagementService.UpdatePermissionsAsync(
            CurrentActorFactory.Create(User),
            new UpdateUserPermissionsCommand(id, request.Permissions ?? []),
            cancellationToken);

        return Ok(ApiResponseFactory.Success(ToDetailResponse(updated)));
    }

    private static UserListItemResponse ToListItemResponse(UserListItemDto dto)
    {
        return new UserListItemResponse(
            Id: dto.Id,
            Username: dto.Username,
            Role: dto.Role,
            IsActive: dto.IsActive,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc,
            LastLoginAtUtc: dto.LastLoginAtUtc,
            Permissions: dto.Permissions);
    }

    private static UserDetailResponse ToDetailResponse(UserDetailDto dto)
    {
        return new UserDetailResponse(
            Id: dto.Id,
            Username: dto.Username,
            Role: dto.Role,
            IsActive: dto.IsActive,
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc,
            LastLoginAtUtc: dto.LastLoginAtUtc,
            Permissions: dto.Permissions);
    }
}

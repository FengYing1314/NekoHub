using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NekoHub.Api.Auth;
using NekoHub.Api.Contracts.Requests;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Auth.Dtos;
using NekoHub.Application.Auth.Services;

namespace NekoHub.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.LoginAsync(request.Username, request.Password, cancellationToken);
        return Ok(ApiResponseFactory.Success(ToResponse(session)));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return Ok(ApiResponseFactory.Success(ToResponse(session)));
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthorizationPolicies.JwtUserRequired)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthorizationPolicies.JwtUserRequired)]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MeAsync(CancellationToken cancellationToken)
    {
        var userId = PrincipalClaimReader.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized(new ApiErrorResponse(
                new ApiError("auth_unauthorized", "Authentication is required.", HttpContext.TraceIdentifier, StatusCodes.Status401Unauthorized)));
        }

        var user = await authService.GetCurrentUserAsync(userId.Value, cancellationToken);
        return Ok(ApiResponseFactory.Success(ToResponse(user)));
    }

    private static AuthTokenResponse ToResponse(AuthSessionDto dto)
    {
        return new AuthTokenResponse(
            AccessToken: dto.AccessToken,
            RefreshToken: dto.RefreshToken,
            AccessTokenExpiresAtUtc: dto.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc: dto.RefreshTokenExpiresAtUtc,
            User: ToResponse(dto.User));
    }

    private static CurrentUserResponse ToResponse(AuthenticatedUserDto dto)
    {
        return new CurrentUserResponse(
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

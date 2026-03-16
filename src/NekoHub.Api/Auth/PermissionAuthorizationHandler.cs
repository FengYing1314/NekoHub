using Microsoft.AspNetCore.Authorization;
using NekoHub.Application.Auth.Services;
using NekoHub.Domain.Users;

namespace NekoHub.Api.Auth;

public sealed class PermissionAuthorizationHandler(IPermissionService permissionService)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (CurrentActorFactory.IsApiKeyPrincipal(context.User))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = PrincipalClaimReader.GetUserId(context.User);
        var roleClaim = PrincipalClaimReader.GetRole(context.User);
        if (userId is null
            || !Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var role))
        {
            return;
        }

        var cancellationToken = ResolveCancellationToken(context);
        if (await permissionService.HasPermissionAsync(userId.Value, role, requirement.Permission, cancellationToken))
        {
            context.Succeed(requirement);
        }
    }

    private static CancellationToken ResolveCancellationToken(AuthorizationHandlerContext context)
    {
        return context.Resource switch
        {
            HttpContext httpContext => httpContext.RequestAborted,
            _ => CancellationToken.None
        };
    }
}

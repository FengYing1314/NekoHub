using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NekoHub.Application.Abstractions.Security;
using NekoHub.Application.Auth;
using NekoHub.Domain.Users;
using NekoHub.Infrastructure.Options;

namespace NekoHub.Infrastructure.Persistence;

public sealed class SuperAdminBootstrapService(
    AssetDbContext dbContext,
    IPasswordHashService passwordHashService,
    IOptions<BootstrapSuperAdminOptions> options,
    ILogger<SuperAdminBootstrapService> logger)
{
    public async Task EnsureBootstrappedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation(
                "Skipped super admin bootstrap because at least one user already exists in the database.");
            return;
        }

        var username = options.Value.Username?.Trim();
        var password = options.Value.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "Skipped super admin bootstrap because {SectionName} credentials are not configured.",
                BootstrapSuperAdminOptions.SectionName);
            return;
        }

        var user = new User(
            id: Guid.CreateVersion7(),
            username: username,
            role: UserRole.SuperAdmin,
            passwordHash: "pending");
        user.SetPasswordHash(passwordHashService.HashPassword(user, password));

        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.UserPermissionGrants.AddRangeAsync(
            PermissionCatalog.All.Select(permission => new UserPermissionGrant(user.Id, permission)),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Bootstrapped super admin account '{Username}'.", user.Username);
    }
}

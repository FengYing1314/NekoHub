using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NekoHub.Application.Auth;
using NekoHub.Domain.Users;
using NekoHub.Infrastructure.Options;
using NekoHub.Infrastructure.Persistence;
using NekoHub.Infrastructure.Security;
using NekoHub.Api.IntegrationTests.Setup;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class SuperAdminBootstrapServiceTests
{
    [Fact]
    public async Task EnsureBootstrappedAsync_When_Database_Is_Empty_Should_Create_SuperAdmin()
    {
        await using var lease = await PostgresTestEnvironment.CreateDatabaseLeaseAsync("bootstrap_empty");
        await using var dbContext = await CreateMigratedDbContextAsync(lease.ConnectionString);

        var service = CreateBootstrapService(dbContext, "bootstrap-admin", "bootstrap-password-123");
        await service.EnsureBootstrappedAsync();

        var users = await dbContext.Users.AsNoTracking().ToListAsync();
        users.Should().ContainSingle();
        users[0].Role.Should().Be(UserRole.SuperAdmin);
        users[0].Username.Should().Be("bootstrap-admin");

        var grants = await dbContext.UserPermissionGrants.AsNoTracking().CountAsync();
        grants.Should().Be(PermissionCatalog.All.Count);
    }

    [Fact]
    public async Task EnsureBootstrappedAsync_When_Any_User_Exists_Should_Skip()
    {
        await using var lease = await PostgresTestEnvironment.CreateDatabaseLeaseAsync("bootstrap_existing_user");
        await using var dbContext = await CreateMigratedDbContextAsync(lease.ConnectionString);

        dbContext.Users.Add(new User(
            id: Guid.CreateVersion7(),
            username: "existing-user",
            passwordHash: "existing-password-hash",
            role: UserRole.User,
            isActive: true));
        await dbContext.SaveChangesAsync();

        var service = CreateBootstrapService(dbContext, "bootstrap-admin", "bootstrap-password-123");
        await service.EnsureBootstrappedAsync();

        var users = await dbContext.Users.AsNoTracking().ToListAsync();
        users.Should().ContainSingle();
        users[0].Username.Should().Be("existing-user");
        users[0].Role.Should().Be(UserRole.User);
    }

    private static SuperAdminBootstrapService CreateBootstrapService(
        AssetDbContext dbContext,
        string username,
        string password)
    {
        return new SuperAdminBootstrapService(
            dbContext,
            new AspNetPasswordHashService(),
            Options.Create(new BootstrapSuperAdminOptions
            {
                Username = username,
                Password = password
            }),
            NullLogger<SuperAdminBootstrapService>.Instance);
    }

    private static async Task<AssetDbContext> CreateMigratedDbContextAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AssetDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var dbContext = new AssetDbContext(options);
        await dbContext.Database.MigrateAsync();
        return dbContext;
    }
}

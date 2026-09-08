using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Domain.Users;
using NekoHub.Infrastructure.Persistence;
using NekoHub.Infrastructure.Persistence.EfCore;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class RefreshTokenRepositoryTests
{
    [Fact]
    public async Task Concurrent_Rotation_Of_The_Same_Snapshot_Should_Commit_Only_One_Successor()
    {
        await using var lease = await PostgresTestEnvironment.CreateDatabaseLeaseAsync("refresh_rotation");
        var (userId, original) = await SeedSessionAsync(lease.ConnectionString);
        await using var firstContext = CreateContext(lease.ConnectionString);
        await using var secondContext = CreateContext(lease.ConnectionString);
        var firstRepository = new EfCoreRefreshTokenRepository(firstContext);
        var secondRepository = new EfCoreRefreshTokenRepository(secondContext);

        // 先让两个独立连接都读到未撤销快照，再并发消费，避免测试依赖请求调度碰巧重叠。
        var firstSnapshot = await firstRepository.GetByTokenHashAsync(original.TokenHash);
        var secondSnapshot = await secondRepository.GetByTokenHashAsync(original.TokenHash);
        var outcomes = await Task.WhenAll(
            firstRepository.TryRotateAsync(firstSnapshot!, CreateToken(userId)),
            secondRepository.TryRotateAsync(secondSnapshot!, CreateToken(userId)));

        outcomes.Count(static succeeded => succeeded).Should().Be(1);
        await using var inspection = CreateContext(lease.ConnectionString);
        var tokens = await inspection.RefreshTokens.AsNoTracking().ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Count(static token => !token.IsRevoked).Should().Be(1);
        tokens.Single(token => token.Id == original.Id).ReplacedByTokenId.Should().Be(
            tokens.Single(static token => !token.IsRevoked).Id);
    }

    [Fact]
    public async Task Rotation_When_Successor_Insert_Fails_Should_Roll_Back_Consumption()
    {
        await using var lease = await PostgresTestEnvironment.CreateDatabaseLeaseAsync("refresh_rollback");
        var (userId, original) = await SeedSessionAsync(lease.ConnectionString);
        await using (var context = CreateContext(lease.ConnectionString))
        {
            var repository = new EfCoreRefreshTokenRepository(context);
            var duplicateHash = new RefreshToken(Guid.CreateVersion7(), userId, original.TokenHash,
                Guid.CreateVersion7().ToString(), DateTimeOffset.UtcNow.AddDays(1));

            var rotate = () => repository.TryRotateAsync(original, duplicateHash);
            await rotate.Should().ThrowAsync<DbUpdateException>();
        }

        await using var inspection = CreateContext(lease.ConnectionString);
        var tokens = await inspection.RefreshTokens.AsNoTracking().ToListAsync();
        tokens.Should().ContainSingle();
        tokens[0].IsRevoked.Should().BeFalse();
        tokens[0].ReplacedByTokenId.Should().BeNull();
    }

    [Fact]
    public async Task Password_Reset_Concurrent_With_Rotation_Should_Leave_No_Active_Successor()
    {
        await using var lease = await PostgresTestEnvironment.CreateDatabaseLeaseAsync("refresh_reset_race");
        var (userId, original) = await SeedSessionAsync(lease.ConnectionString);
        await using var resetContext = CreateContext(lease.ConnectionString);
        await using var rotationContext = CreateContext(lease.ConnectionString);
        var user = await resetContext.Users.SingleAsync(user => user.Id == userId);
        user.SetPasswordHash("new-password-hash");
        var resetRepository = new EfCoreRefreshTokenRepository(resetContext);
        var rotationRepository = new EfCoreRefreshTokenRepository(rotationContext);
        var staleSnapshot = await rotationRepository.GetByTokenHashAsync(original.TokenHash);

        await Task.WhenAll(
            resetRepository.RevokeAllAndSaveChangesAsync(userId),
            rotationRepository.TryRotateAsync(staleSnapshot!, CreateToken(userId)));

        await using var inspection = CreateContext(lease.ConnectionString);
        (await inspection.RefreshTokens.CountAsync(token => token.RevokedAtUtc == null)).Should().Be(0);
        (await inspection.Users.SingleAsync(user => user.Id == userId)).PasswordHash.Should().Be("new-password-hash");
    }

    [Fact]
    public async Task Login_Verified_Before_Password_Reset_Should_Not_Issue_A_New_Session_After_Reset()
    {
        await using var lease = await PostgresTestEnvironment.CreateDatabaseLeaseAsync("refresh_stale_login");
        var (userId, _) = await SeedSessionAsync(lease.ConnectionString);
        await using var loginContext = CreateContext(lease.ConnectionString);
        var staleUser = await loginContext.Users.SingleAsync(user => user.Id == userId);
        staleUser.RecordLogin();
        await using (var resetContext = CreateContext(lease.ConnectionString))
        {
            var currentUser = await resetContext.Users.SingleAsync(user => user.Id == userId);
            currentUser.SetPasswordHash("new-password-hash");
            await new EfCoreRefreshTokenRepository(resetContext).RevokeAllAndSaveChangesAsync(userId);
        }

        var issued = await new EfCoreRefreshTokenRepository(loginContext).TryIssueAsync(staleUser, CreateToken(userId));

        issued.Should().BeFalse();
        await using var inspection = CreateContext(lease.ConnectionString);
        (await inspection.RefreshTokens.CountAsync(token => token.RevokedAtUtc == null)).Should().Be(0);
    }

    private static async Task<(Guid UserId, RefreshToken Token)> SeedSessionAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();
        var user = new User(Guid.CreateVersion7(), "refresh-user", "initial-password-hash", UserRole.User);
        var token = CreateToken(user.Id);
        context.Users.Add(user);
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();
        return (user.Id, token);
    }

    private static RefreshToken CreateToken(Guid userId)
    {
        return new RefreshToken(Guid.CreateVersion7(), userId, Guid.CreateVersion7().ToString(),
            Guid.CreateVersion7().ToString(), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static AssetDbContext CreateContext(string connectionString)
    {
        return new AssetDbContext(new DbContextOptionsBuilder<AssetDbContext>().UseNpgsql(connectionString).Options);
    }
}

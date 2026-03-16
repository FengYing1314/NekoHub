using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Domain.Storage;
using NekoHub.Infrastructure.Persistence;
using NekoHub.Infrastructure.Persistence.EfCore;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class StorageProviderProfilePersistenceTests
{
    [Fact]
    public async Task StorageProviderProfile_Should_Persist_And_Query_Basic_Fields()
    {
        await using var databaseLease = await PostgresTestEnvironment.CreateDatabaseLeaseAsync("profile_persistence");
        var options = new DbContextOptionsBuilder<AssetDbContext>()
            .UseNpgsql(databaseLease.ConnectionString)
            .Options;

        await using var dbContext = new AssetDbContext(options);
        await dbContext.Database.MigrateAsync();

        var repository = new EfCoreStorageProviderProfileRepository(dbContext);
        var profile = new StorageProviderProfile(
            id: Guid.CreateVersion7(),
            name: "local-primary",
            providerType: StorageProviderTypes.Local,
            configurationJson: """{"rootPath":"storage/assets"}""",
            capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.Local),
            displayName: "Local Primary",
            isEnabled: true,
            isDefault: true,
            secretConfigurationJson: """{"note":"placeholder"}""");

        await repository.AddAsync(profile);
        await repository.SaveChangesAsync();

        var byId = await repository.GetByIdAsync(profile.Id);
        var byName = await repository.GetByNameAsync(profile.Name);
        var @default = await repository.GetDefaultAsync();
        var all = await repository.ListAsync();

        byId.Should().NotBeNull();
        byName.Should().NotBeNull();
        @default.Should().NotBeNull();
        all.Should().ContainSingle();

        byId!.Name.Should().Be("local-primary");
        byId.DisplayName.Should().Be("Local Primary");
        byId.ProviderType.Should().Be(StorageProviderTypes.Local);
        byId.ConfigurationJson.Should().Be("""{"rootPath":"storage/assets"}""");
        byId.SecretConfigurationJson.Should().Be("""{"note":"placeholder"}""");
        byId.GetCapabilities().Should().Be(StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.Local));
        byId.IsPlatformBacked.Should().BeFalse();
        byId.IsExperimental.Should().BeFalse();
        byId.RequiresTokenForPrivateRead.Should().BeFalse();

        byName!.Id.Should().Be(profile.Id);
        @default!.Id.Should().Be(profile.Id);
    }

    [Fact]
    public async Task StorageProviderProfile_Should_Persist_GitHub_Capability_Flags()
    {
        await using var databaseLease = await PostgresTestEnvironment.CreateDatabaseLeaseAsync("profile_capability_flags");
        var options = new DbContextOptionsBuilder<AssetDbContext>()
            .UseNpgsql(databaseLease.ConnectionString)
            .Options;

        await using var dbContext = new AssetDbContext(options);
        await dbContext.Database.MigrateAsync();

        var repository = new EfCoreStorageProviderProfileRepository(dbContext);
        var profile = new StorageProviderProfile(
            id: Guid.CreateVersion7(),
            name: "gh-repo",
            providerType: StorageProviderTypes.GitHubRepo,
            configurationJson: """{"owner":"nekohub","repo":"assets-repo","ref":"main","basePath":"media"}""",
            capabilities: StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.GitHubRepo),
            displayName: "GitHub Repo",
            isEnabled: true,
            isDefault: false,
            secretConfigurationJson: """{"token":"ghp_placeholder"}""");

        await repository.AddAsync(profile);
        await repository.SaveChangesAsync();

        var byId = await repository.GetByIdAsync(profile.Id);
        byId.Should().NotBeNull();
        byId!.ProviderType.Should().Be(StorageProviderTypes.GitHubRepo);
        byId.GetCapabilities().IsPlatformBacked.Should().BeTrue();
        byId.GetCapabilities().IsExperimental.Should().BeTrue();
        byId.GetCapabilities().SupportsPrivateRead.Should().BeTrue();
        byId.GetCapabilities().RecommendedForPrimaryStorage.Should().BeFalse();
        byId.GetCapabilities().RequiresTokenForPrivateRead.Should().BeTrue();
    }
}

using Microsoft.EntityFrameworkCore;
using NekoHub.Domain.Ai;
using NekoHub.Domain.Assets;
using NekoHub.Domain.Storage;
using NekoHub.Domain.Skills;
using NekoHub.Domain.Users;
using NekoHub.Domain.Workflows;

namespace NekoHub.Infrastructure.Persistence;

public sealed class AssetDbContext(DbContextOptions<AssetDbContext> options) : DbContext(options)
{
    public DbSet<AiProviderProfile> AiProviderProfiles => Set<AiProviderProfile>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetDerivative> AssetDerivatives => Set<AssetDerivative>();
    public DbSet<AssetStructuredResult> AssetStructuredResults => Set<AssetStructuredResult>();
    public DbSet<StorageProviderProfile> StorageProviderProfiles => Set<StorageProviderProfile>();
    public DbSet<SkillExecution> SkillExecutions => Set<SkillExecution>();
    public DbSet<SkillExecutionStepResult> SkillExecutionStepResults => Set<SkillExecutionStepResult>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserPermissionGrant> UserPermissionGrants => Set<UserPermissionGrant>();
    public DbSet<WorkflowProfile> WorkflowProfiles => Set<WorkflowProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssetDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

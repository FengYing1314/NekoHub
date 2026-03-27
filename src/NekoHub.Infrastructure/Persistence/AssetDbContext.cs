using Microsoft.EntityFrameworkCore;
using NekoHub.Domain.Assets;
using NekoHub.Domain.Storage;
using NekoHub.Domain.Skills;

namespace NekoHub.Infrastructure.Persistence;

public sealed class AssetDbContext(DbContextOptions<AssetDbContext> options) : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetDerivative> AssetDerivatives => Set<AssetDerivative>();
    public DbSet<AssetStructuredResult> AssetStructuredResults => Set<AssetStructuredResult>();
    public DbSet<StorageProviderProfile> StorageProviderProfiles => Set<StorageProviderProfile>();
    public DbSet<SkillExecution> SkillExecutions => Set<SkillExecution>();
    public DbSet<SkillExecutionStepResult> SkillExecutionStepResults => Set<SkillExecutionStepResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssetDbContext).Assembly);
        ApplyDateTimeOffsetConverters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private static void ApplyDateTimeOffsetConverters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var properties = entityType.GetProperties()
                .Where(property => property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?));

            foreach (var property in properties)
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(SqliteDateTimeOffsetConverters.DateTimeOffsetToUtcTicks);
                }
                else
                {
                    property.SetValueConverter(SqliteDateTimeOffsetConverters.NullableDateTimeOffsetToUtcTicks);
                }
            }
        }
    }
}

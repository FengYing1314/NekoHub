using Microsoft.EntityFrameworkCore;
using NekoHub.Domain.Assets;

namespace NekoHub.Infrastructure.Persistence;

public sealed class AssetDbContext(DbContextOptions<AssetDbContext> options) : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetDerivative> AssetDerivatives => Set<AssetDerivative>();
    public DbSet<AssetStructuredResult> AssetStructuredResults => Set<AssetStructuredResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssetDbContext).Assembly);

        if (Database.IsSqlite())
        {
            ApplySqliteDateTimeOffsetConverters(modelBuilder);
        }

        base.OnModelCreating(modelBuilder);
    }

    private static void ApplySqliteDateTimeOffsetConverters(ModelBuilder modelBuilder)
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

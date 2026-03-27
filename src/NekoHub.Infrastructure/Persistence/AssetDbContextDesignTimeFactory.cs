using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NekoHub.Infrastructure.Persistence;

public sealed class AssetDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AssetDbContext>
{
    public AssetDbContext CreateDbContext(string[] args)
    {
        var apiProjectPath = ResolveApiProjectPath();
        var providerFromArguments = GetArgumentValue(args, "Persistence:Database:Provider")
            ?? GetArgumentValue(args, "Persistence__Database__Provider");
        var connectionStringFromArguments = GetArgumentValue(args, "Persistence:Database:ConnectionString")
            ?? GetArgumentValue(args, "Persistence__Database__ConnectionString");

        // Migrations are generated against PostgreSQL by default (compose default),
        // and can be overridden explicitly via Persistence__Database__Provider.
        var provider = DatabaseProviderNames.Normalize(
            providerFromArguments
            ?? Environment.GetEnvironmentVariable("Persistence__Database__Provider")
            ?? Environment.GetEnvironmentVariable("Persistence:Database:Provider")
            ?? DatabaseProviderNames.PostgreSql);

        var connectionString = connectionStringFromArguments
            ?? Environment.GetEnvironmentVariable("Persistence__Database__ConnectionString")
            ?? Environment.GetEnvironmentVariable("Persistence:Database:ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = provider == DatabaseProviderNames.Sqlite
                ? "Data Source=storage/nekohub.db"
                : "Host=localhost;Port=5432;Database=nekohub;Username=nekohub;Password=nekohub-dev";
        }

        var optionsBuilder = new DbContextOptionsBuilder<AssetDbContext>();
        if (provider == DatabaseProviderNames.Sqlite)
        {
            var resolvedSqliteConnectionString = SqliteConnectionStringResolver.Resolve(connectionString, apiProjectPath);
            optionsBuilder.UseSqlite(resolvedSqliteConnectionString);
        }
        else
        {
            optionsBuilder.UseNpgsql(connectionString);
        }

        return new AssetDbContext(optionsBuilder.Options);
    }

    private static string? GetArgumentValue(IReadOnlyList<string> args, string key)
    {
        var normalizedKey = key.Trim();
        if (normalizedKey.Length == 0)
        {
            return null;
        }

        foreach (var argument in args)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            var equalsIndex = argument.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex >= argument.Length - 1)
            {
                continue;
            }

            var argumentKey = argument[..equalsIndex].Trim().TrimStart('-');
            if (!string.Equals(argumentKey, normalizedKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return argument[(equalsIndex + 1)..].Trim();
        }

        return null;
    }

    private static string ResolveApiProjectPath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var candidate = Path.GetFullPath(Path.Combine(currentDirectory, "..", "NekoHub.Api"));

        if (File.Exists(Path.Combine(candidate, "NekoHub.Api.csproj")))
        {
            return candidate;
        }

        return currentDirectory;
    }
}

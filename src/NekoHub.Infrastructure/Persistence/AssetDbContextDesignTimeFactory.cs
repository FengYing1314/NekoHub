using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NekoHub.Infrastructure.Persistence;

public sealed class AssetDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AssetDbContext>
{
    public AssetDbContext CreateDbContext(string[] args)
    {
        var providerFromArguments = GetArgumentValue(args, "Persistence:Database:Provider")
            ?? GetArgumentValue(args, "Persistence__Database__Provider");
        var connectionStringFromArguments = GetArgumentValue(args, "Persistence:Database:ConnectionString")
            ?? GetArgumentValue(args, "Persistence__Database__ConnectionString");

        DatabaseProviderNames.Normalize(
            providerFromArguments
            ?? Environment.GetEnvironmentVariable("Persistence__Database__Provider")
            ?? Environment.GetEnvironmentVariable("Persistence:Database:Provider")
            ?? DatabaseProviderNames.PostgreSql);

        var connectionString = connectionStringFromArguments
            ?? Environment.GetEnvironmentVariable("Persistence__Database__ConnectionString")
            ?? Environment.GetEnvironmentVariable("Persistence:Database:ConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Host=localhost;Port=5432;Database=nekohub;Username=nekohub;Password=nekohub-dev";
        }

        var optionsBuilder = new DbContextOptionsBuilder<AssetDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

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
}

using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NekoHub.Application.Storage.Commands;
using NekoHub.Application.Storage.Services;
using NekoHub.Domain.Storage;
using NekoHub.Infrastructure.Options;

namespace NekoHub.Infrastructure.Persistence;

public static class AssetPersistenceInitializationExtensions
{
    public static void InitializeAssetPersistence(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        var databaseOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var normalizedProvider = DatabaseProviderNames.Normalize(databaseOptions.Provider);

        if (string.Equals(normalizedProvider, DatabaseProviderNames.Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            var hostEnvironment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
            var resolvedConnectionString = SqliteConnectionStringResolver.Resolve(
                databaseOptions.ConnectionString,
                hostEnvironment.ContentRootPath);

            EnsureSqliteDirectory(normalizedProvider, resolvedConnectionString);
            TryBaselineLegacySqliteSchema(dbContext, normalizedProvider);
        }

        dbContext.Database.Migrate();
        EnsureDefaultWriteProfileBootstrap(scope.ServiceProvider, dbContext);
    }

    private static void EnsureDefaultWriteProfileBootstrap(IServiceProvider serviceProvider, AssetDbContext dbContext)
    {
        var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
        var logger = serviceProvider
            .GetService<ILoggerFactory>()
            ?.CreateLogger("NekoHub.StorageProfileBootstrap");

        if (hostEnvironment.IsEnvironment("Testing"))
        {
            return;
        }

        if (dbContext.StorageProviderProfiles.Any(x => x.IsDefault))
        {
            return;
        }

        var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
        var localOptions = serviceProvider.GetRequiredService<IOptions<LocalStorageOptions>>().Value;
        var s3Options = serviceProvider.GetRequiredService<IOptions<S3StorageOptions>>().Value;

        var seed = BuildBootstrapSeed(storageOptions, localOptions, s3Options);
        if (seed is null)
        {
            logger?.LogInformation(
                "Skipped default write profile bootstrap because runtime provider '{Provider}' is not in bootstrap scope.",
                storageOptions.Provider);
            return;
        }

        var existingNames = dbContext.StorageProviderProfiles
            .AsNoTracking()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var profileName = ResolveUniqueProfileName(seed.BaseName, existingNames);
        var profileManagementService = serviceProvider.GetRequiredService<IStorageProviderProfileManagementService>();
        var createCommand = new CreateStorageProviderProfileCommand(
            Name: profileName,
            DisplayName: seed.DisplayName,
            ProviderType: seed.ProviderType,
            IsEnabled: true,
            IsDefault: true,
            Configuration: JsonSerializer.SerializeToElement(seed.Configuration),
            SecretConfiguration: seed.SecretConfiguration is null
                ? null
                : JsonSerializer.SerializeToElement(seed.SecretConfiguration));

        // Bootstrap only initializes DB-side management defaults. It does not change runtime provider selection.
        profileManagementService.CreateAsync(createCommand).GetAwaiter().GetResult();

        logger?.LogInformation(
            "Bootstrapped default write profile '{ProfileName}' for provider type '{ProviderType}'.",
            profileName,
            seed.ProviderType);
    }

    private static BootstrapSeed? BuildBootstrapSeed(
        StorageOptions storageOptions,
        LocalStorageOptions localOptions,
        S3StorageOptions s3Options)
    {
        var configuredProvider = (storageOptions.Provider ?? string.Empty).Trim().ToLowerInvariant();

        if (configuredProvider == StorageProviderTypes.Local || configuredProvider == "local")
        {
            var localConfiguration = new Dictionary<string, object?>
            {
                ["rootPath"] = localOptions.RootPath,
                ["createDirectoryIfMissing"] = localOptions.CreateDirectoryIfMissing,
            };

            if (!string.IsNullOrWhiteSpace(storageOptions.PublicBaseUrl))
            {
                localConfiguration["publicBaseUrl"] = storageOptions.PublicBaseUrl;
            }

            return new BootstrapSeed(
                BaseName: "local-default",
                DisplayName: "Local Default Write Profile",
                ProviderType: StorageProviderTypes.Local,
                Configuration: localConfiguration,
                SecretConfiguration: null);
        }

        if (configuredProvider == StorageProviderTypes.S3Compatible || configuredProvider == "s3")
        {
            var s3Configuration = new Dictionary<string, object?>
            {
                ["providerName"] = string.IsNullOrWhiteSpace(s3Options.ProviderName) ? "s3" : s3Options.ProviderName,
                ["endpoint"] = s3Options.Endpoint,
                ["bucket"] = s3Options.Bucket,
                ["region"] = s3Options.Region,
                ["forcePathStyle"] = s3Options.ForcePathStyle,
            };

            var publicBaseUrl = !string.IsNullOrWhiteSpace(storageOptions.PublicBaseUrl)
                ? storageOptions.PublicBaseUrl
                : s3Options.PublicBaseUrl;
            if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            {
                s3Configuration["publicBaseUrl"] = publicBaseUrl;
            }

            var s3SecretConfiguration = new Dictionary<string, object?>
            {
                ["accessKey"] = s3Options.AccessKey,
                ["secretKey"] = s3Options.SecretKey,
            };

            return new BootstrapSeed(
                BaseName: "s3-default",
                DisplayName: "S3 Default Write Profile",
                ProviderType: StorageProviderTypes.S3Compatible,
                Configuration: s3Configuration,
                SecretConfiguration: s3SecretConfiguration);
        }

        return null;
    }

    private static string ResolveUniqueProfileName(string baseName, IReadOnlySet<string> existingNames)
    {
        if (!existingNames.Contains(baseName))
        {
            return baseName;
        }

        for (var suffix = 2; suffix <= 999; suffix += 1)
        {
            var candidate = $"{baseName}-{suffix}";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Failed to resolve unique bootstrap profile name from base '{baseName}'.");
    }

    private sealed record BootstrapSeed(
        string BaseName,
        string DisplayName,
        string ProviderType,
        IReadOnlyDictionary<string, object?> Configuration,
        IReadOnlyDictionary<string, object?>? SecretConfiguration);

    private static void EnsureSqliteDirectory(string provider, string connectionString)
    {
        if (!string.Equals(provider, "sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var directory = SqliteConnectionStringResolver.TryGetDatabaseDirectory(connectionString);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void TryBaselineLegacySqliteSchema(AssetDbContext dbContext, string provider)
    {
        if (!string.Equals(provider, "sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var migrations = dbContext.Database.GetMigrations().ToList();
        if (migrations.Count == 0)
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            connection.Open();
        }

        try
        {
            var hasAssetsTable = HasSqliteTable(connection, "Assets");
            if (!hasAssetsTable)
            {
                return;
            }

            EnsureMigrationHistoryTable(connection);
            var hasAnyHistory = HasMigrationHistory(connection);
            if (hasAnyHistory)
            {
                return;
            }

            var initialMigration = migrations[0];
            var productVersion = dbContext.Model.GetProductVersion() ?? "10.0.3";
            InsertMigrationHistory(connection, initialMigration, productVersion);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                connection.Close();
            }
        }
    }

    private static bool HasSqliteTable(System.Data.Common.DbConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = command.ExecuteScalar();
        return Convert.ToInt64(result) > 0;
    }

    private static void EnsureMigrationHistoryTable(System.Data.Common.DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                                  "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                                  "ProductVersion" TEXT NOT NULL
                              );
                              """;
        command.ExecuteNonQuery();
    }

    private static bool HasMigrationHistory(System.Data.Common.DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\"";
        var result = command.ExecuteScalar();
        return Convert.ToInt64(result) > 0;
    }

    private static void InsertMigrationHistory(
        System.Data.Common.DbConnection connection,
        string migrationId,
        string productVersion)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                              VALUES ($migrationId, $productVersion);
                              """;

        var migrationIdParameter = command.CreateParameter();
        migrationIdParameter.ParameterName = "$migrationId";
        migrationIdParameter.Value = migrationId;
        command.Parameters.Add(migrationIdParameter);

        var productVersionParameter = command.CreateParameter();
        productVersionParameter.ParameterName = "$productVersion";
        productVersionParameter.Value = productVersion;
        command.Parameters.Add(productVersionParameter);

        command.ExecuteNonQuery();
    }
}

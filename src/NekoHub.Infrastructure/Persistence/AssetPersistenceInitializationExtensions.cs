using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    }

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

using System.Net.Sockets;
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
using Npgsql;

namespace NekoHub.Infrastructure.Persistence;

public static class AssetPersistenceInitializationExtensions
{
    private static readonly TimeSpan PostgreSqlMigrationRetryDelay = TimeSpan.FromSeconds(2);
    private const int PostgreSqlMigrationMaxAttempts = 10;

    public static void InitializeAssetPersistence(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        var databaseOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var normalizedProvider = DatabaseProviderNames.Normalize(databaseOptions.Provider);
        var logger = scope.ServiceProvider
            .GetService<ILoggerFactory>()
            ?.CreateLogger("NekoHub.PersistenceInitialization");

        logger?.LogInformation(
            "Initializing asset persistence with database provider '{DatabaseProvider}'.",
            normalizedProvider);

        ApplyMigrationsWithRetry(dbContext, logger);
        EnsureDefaultWriteProfileBootstrap(scope.ServiceProvider, dbContext);

        logger?.LogInformation("Asset persistence initialization completed.");
    }

    private static void ApplyMigrationsWithRetry(
        AssetDbContext dbContext,
        ILogger? logger)
    {
        for (var attempt = 1; attempt <= PostgreSqlMigrationMaxAttempts; attempt += 1)
        {
            try
            {
                dbContext.Database.Migrate();
                logger?.LogInformation("Database migrations applied successfully on attempt {Attempt}.", attempt);
                return;
            }
            catch (Exception ex) when (IsMissingKerberosDependency(ex))
            {
                logger?.LogCritical(
                    ex,
                    "Failed to initialize database because required system dependency 'libgssapi_krb5.so.2' is missing. Install Debian package 'libgssapi-krb5-2' in runtime image.");
                throw;
            }
            catch (Exception ex) when (attempt < PostgreSqlMigrationMaxAttempts && IsTransientDatabaseStartupFailure(ex))
            {
                logger?.LogWarning(
                    ex,
                    "Database migration attempt {Attempt}/{MaxAttempts} failed due to transient startup/connectivity issue. Retrying in {DelaySeconds}s.",
                    attempt,
                    PostgreSqlMigrationMaxAttempts,
                    PostgreSqlMigrationRetryDelay.TotalSeconds);
                Thread.Sleep(PostgreSqlMigrationRetryDelay);
            }
        }

        throw new InvalidOperationException(
            $"Failed to apply database migrations after {PostgreSqlMigrationMaxAttempts} attempts.");
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

    private static bool IsTransientDatabaseStartupFailure(Exception exception)
    {
        if (exception is TimeoutException)
        {
            return true;
        }

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException)
            {
                return true;
            }

            if (current is NpgsqlException)
            {
                return true;
            }

            if (current is PostgresException postgresException &&
                string.Equals(postgresException.SqlState, "57P03", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMissingKerberosDependency(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DllNotFoundException dllNotFoundException &&
                dllNotFoundException.Message.Contains("libgssapi_krb5.so.2", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (current.Message.Contains("libgssapi_krb5.so.2", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record BootstrapSeed(
        string BaseName,
        string DisplayName,
        string ProviderType,
        IReadOnlyDictionary<string, object?> Configuration,
        IReadOnlyDictionary<string, object?>? SecretConfiguration);

}

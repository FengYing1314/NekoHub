using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NekoHub.Application.Abstractions.Metadata;
using NekoHub.Application.Abstractions.Persistence;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Skills;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Infrastructure.Metadata;
using NekoHub.Infrastructure.Options;
using NekoHub.Infrastructure.Persistence;
using NekoHub.Infrastructure.Persistence.EfCore;
using NekoHub.Infrastructure.Processing;
using NekoHub.Infrastructure.Skills;
using NekoHub.Infrastructure.Skills.Steps;
using NekoHub.Infrastructure.Storage;
using NekoHub.Infrastructure.Storage.GitHub;
using NekoHub.Infrastructure.Storage.Local;
using NekoHub.Infrastructure.Storage.S3;

namespace NekoHub.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services
            .AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.Provider),
                "Storage:Provider is required.")
            .ValidateOnStart();
        services
            .AddOptions<S3StorageOptions>()
            .Bind(configuration.GetSection(S3StorageOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<S3StorageOptions>, S3StorageOptionsValidator>();
        services
            .AddOptions<GitHubRepoStorageOptions>()
            .Bind(configuration.GetSection(GitHubRepoStorageOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GitHubRepoStorageOptions>, GitHubRepoStorageOptionsValidator>();
        services
            .AddOptions<LocalStorageOptions>()
            .Bind(configuration.GetSection(LocalStorageOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<LocalStorageOptions>, LocalStorageOptionsValidator>();
        services.AddHttpClient(GitHubRepoAssetStorage.HttpClientName);

        services.AddDbContext<AssetDbContext>((serviceProvider, dbContextOptions) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
            if (string.IsNullOrWhiteSpace(databaseOptions.ConnectionString))
            {
                throw new InvalidOperationException("Persistence:Database:ConnectionString is required.");
            }

            var normalizedProvider = DatabaseProviderNames.Normalize(databaseOptions.Provider);
            if (string.Equals(normalizedProvider, DatabaseProviderNames.Sqlite, StringComparison.OrdinalIgnoreCase))
            {
                var resolvedConnectionString = SqliteConnectionStringResolver.Resolve(
                    databaseOptions.ConnectionString,
                    hostEnvironment.ContentRootPath);

                dbContextOptions.UseSqlite(resolvedConnectionString);
                dbContextOptions.ConfigureWarnings(warnings =>
                    warnings.Log(RelationalEventId.PendingModelChangesWarning));
                return;
            }

            dbContextOptions.UseNpgsql(databaseOptions.ConnectionString);
        });

        services.AddSingleton<LocalAssetStorage>();
        services.AddSingleton<S3AssetStorage>();
        services.AddSingleton<GitHubRepoAssetStorage>();
        services.AddSingleton<IAssetStorage>(serviceProvider =>
            serviceProvider.GetRequiredService<LocalAssetStorage>());
        services.AddSingleton<IAssetStorage>(serviceProvider =>
            serviceProvider.GetRequiredService<S3AssetStorage>());
        services.AddSingleton<IAssetStorage>(serviceProvider =>
            serviceProvider.GetRequiredService<GitHubRepoAssetStorage>());
        services.AddSingleton<IGitHubRepoStorage>(serviceProvider =>
            serviceProvider.GetRequiredService<GitHubRepoAssetStorage>());
        services.AddSingleton<IGitHubRepoProfileStorageInvoker, GitHubRepoProfileStorageInvoker>();
        services.AddSingleton<IAssetStorageResolver, AssetStorageResolver>();
        services.AddSingleton<IAssetMetadataExtractor, BasicAssetMetadataExtractor>();
        services.AddScoped<IAssetProcessingDispatcher, AssetProcessingDispatcher>();
        services.AddSingleton<IAssetSkillDefinitionProvider, DefaultAssetSkillDefinitionProvider>();
        services.AddScoped<ISkillRunner, SkillRunner>();
        services.AddScoped<ThumbnailAssetPostProcessor>();
        services.AddScoped<BasicCaptionStructuredResultPostProcessor>();
        services.AddScoped<ISkillStepExecutor, GenerateThumbnailSkillStep>();
        services.AddScoped<ISkillStepExecutor, GenerateBasicCaptionSkillStep>();
        services.AddScoped<IAssetRepository, EfCoreAssetRepository>();
        services.AddScoped<IAssetDerivativeRepository, EfCoreAssetDerivativeRepository>();
        services.AddScoped<IAssetStructuredResultRepository, EfCoreAssetStructuredResultRepository>();
        services.AddScoped<IAssetSkillExecutionRepository, EfCoreAssetSkillExecutionRepository>();
        services.AddScoped<IStorageProviderProfileRepository, EfCoreStorageProviderProfileRepository>();

        return services;
    }
}

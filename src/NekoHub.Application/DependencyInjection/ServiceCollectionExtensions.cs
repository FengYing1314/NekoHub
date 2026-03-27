using Microsoft.Extensions.DependencyInjection;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Skills.Services;
using NekoHub.Application.Storage.Services;
using NekoHub.Application.Storage.Validation;

namespace NekoHub.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAssetCommandService, AssetCommandService>();
        services.AddScoped<IAssetQueryService, AssetQueryService>();
        services.AddScoped<IAssetContentService, AssetContentService>();
        services.AddScoped<IAssetStorageTargetSelector, AssetStorageTargetSelector>();
        services.AddScoped<IAssetSkillService, AssetSkillService>();
        services.AddScoped<IStorageProviderQueryService, StorageProviderQueryService>();
        services.AddScoped<IStorageProviderProfileManagementService, StorageProviderProfileManagementService>();
        services.AddScoped<IGitHubRepoProfileAccessService, GitHubRepoProfileAccessService>();
        services.AddSingleton<IStorageProviderProfileConfigurationValidator, LocalStorageProviderProfileConfigurationValidator>();
        services.AddSingleton<IStorageProviderProfileConfigurationValidator, S3CompatibleStorageProviderProfileConfigurationValidator>();
        services.AddSingleton<IStorageProviderProfileConfigurationValidator, GitHubReleasesStorageProviderProfileConfigurationValidator>();
        services.AddSingleton<IStorageProviderProfileConfigurationValidator, GitHubRepoStorageProviderProfileConfigurationValidator>();

        return services;
    }
}

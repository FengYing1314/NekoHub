using Microsoft.Extensions.DependencyInjection;
using NekoHub.Application.Auth.Services;
using NekoHub.Application.Ai.Services;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Skills.Services;
using NekoHub.Application.Storage.Services;
using NekoHub.Application.Storage.Validation;
using NekoHub.Application.Users.Services;
using NekoHub.Application.Workflows.Parsing;
using NekoHub.Application.Workflows.Services;

namespace NekoHub.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IAiProviderProfileService, AiProviderProfileService>();
        services.AddScoped<IAssetCommandService, AssetCommandService>();
        services.AddScoped<IAssetQueryService, AssetQueryService>();
        services.AddScoped<IAssetContentService, AssetContentService>();
        services.AddScoped<IAssetStorageTargetSelector, AssetStorageTargetSelector>();
        services.AddScoped<IAssetSkillService, AssetSkillService>();
        services.AddScoped<IStorageProviderQueryService, StorageProviderQueryService>();
        services.AddScoped<IStorageProviderProfileManagementService, StorageProviderProfileManagementService>();
        services.AddScoped<IGitHubRepoProfileAccessService, GitHubRepoProfileAccessService>();
        services.AddScoped<IWorkflowProfileService, WorkflowProfileService>();
        services.AddScoped<IAssetWorkflowExecutionService, AssetWorkflowExecutionService>();
        services.AddScoped<IAssetSkillDefinitionResolver, AssetSkillDefinitionResolver>();
        services.AddSingleton<IWorkflowGraphParser, WorkflowGraphParser>();
        services.AddSingleton<IStorageProviderProfileConfigurationValidator, LocalStorageProviderProfileConfigurationValidator>();
        services.AddSingleton<IStorageProviderProfileConfigurationValidator, S3CompatibleStorageProviderProfileConfigurationValidator>();
        services.AddSingleton<IStorageProviderProfileConfigurationValidator, GitHubReleasesStorageProviderProfileConfigurationValidator>();
        services.AddSingleton<IStorageProviderProfileConfigurationValidator, GitHubRepoStorageProviderProfileConfigurationValidator>();

        return services;
    }
}

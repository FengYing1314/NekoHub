using Microsoft.Extensions.DependencyInjection;
using NekoHub.Application.Assets.Services;
using NekoHub.Application.Skills.Services;

namespace NekoHub.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAssetCommandService, AssetCommandService>();
        services.AddScoped<IAssetQueryService, AssetQueryService>();
        services.AddScoped<IAssetContentService, AssetContentService>();
        services.AddScoped<IAssetSkillService, AssetSkillService>();

        return services;
    }
}

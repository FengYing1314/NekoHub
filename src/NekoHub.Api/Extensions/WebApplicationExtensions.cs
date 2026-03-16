using NekoHub.Api.Middleware;
using NekoHub.Api.Configuration;
using NekoHub.Application.Assets.Services;
using NekoHub.Infrastructure.Persistence;

namespace NekoHub.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiLayer(this WebApplication app)
    {
        // 在对外开放路由前先完成迁移、默认 profile 和 bootstrap admin 初始化，避免首个请求撞到半初始化状态。
        app.Services.InitializeAssetPersistence();
        app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        app.UseHttpsRedirection();
        app.UseCors(ApiCorsDefaults.PolicyName);
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapOpenApi("/openapi/{documentName}.json");
        app.MapPublicContent();
        app.MapControllers();

        return app;
    }

    private static void MapPublicContent(this WebApplication app)
    {
        app.MapMethods("/content/{**storageKey}", ["GET", "HEAD"], async (
                string storageKey,
                IAssetContentService assetContentService,
                CancellationToken cancellationToken) =>
        {
            // 公开内容路由保持匿名，真正的“是否公开”判断统一收敛在 AssetContentService 中。
            var publicContent = await assetContentService.OpenPublicContentAsync(storageKey, cancellationToken);
            return Results.File(publicContent.Content, publicContent.ContentType, enableRangeProcessing: true);
        })
        .AllowAnonymous();
    }
}

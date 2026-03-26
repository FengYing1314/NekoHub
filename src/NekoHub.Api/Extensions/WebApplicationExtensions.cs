using NekoHub.Api.Middleware;
using NekoHub.Api.Configuration;
using NekoHub.Application.Assets.Services;
using NekoHub.Infrastructure.Persistence;

namespace NekoHub.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiLayer(this WebApplication app)
    {
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
            var publicContent = await assetContentService.OpenPublicContentAsync(storageKey, cancellationToken);
            return Results.File(publicContent.Content, publicContent.ContentType, enableRangeProcessing: true);
        })
        .AllowAnonymous();
    }
}

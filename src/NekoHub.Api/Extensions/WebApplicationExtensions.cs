using NekoHub.Api.Middleware;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Infrastructure.Options;
using NekoHub.Infrastructure.Persistence;
using NekoHub.Infrastructure.Storage.Local;

namespace NekoHub.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiLayer(this WebApplication app)
    {
        app.Services.InitializeAssetPersistence();
        var defaultStorage = app.Services.GetRequiredService<IAssetStorageResolver>().ResolveDefault();
        app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        app.UseLocalStoragePublicContent(defaultStorage);
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapOpenApi("/openapi/{documentName}.json");
        app.MapControllers();

        return app;
    }

    private static void UseLocalStoragePublicContent(this WebApplication app, IAssetStorage defaultStorage)
    {
        if (defaultStorage is not LocalAssetStorage)
        {
            return;
        }

        var localOptions = app.Services.GetRequiredService<IOptions<LocalStorageOptions>>().Value;
        var rootPath = Path.GetFullPath(localOptions.RootPath);

        if (localOptions.CreateDirectoryIfMissing)
        {
            Directory.CreateDirectory(rootPath);
        }

        // 第一版不做文件流代理，直接把本地存储目录映射到 /content 供 publicUrl 访问。
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(rootPath),
            RequestPath = "/content"
        });
    }
}

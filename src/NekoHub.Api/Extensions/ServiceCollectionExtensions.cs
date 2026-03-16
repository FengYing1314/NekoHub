using System.Net.Http.Headers;
using System.Security.Claims;
using System.IO;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NekoHub.Api.Auth;
using NekoHub.Api.Mcp;
using NekoHub.Api.Mcp.Prompts;
using NekoHub.Api.Mcp.Resources;
using NekoHub.Api.Mcp.Tools;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.Configuration;
using NekoHub.Api.Serialization;
using NekoHub.Api.Security;
using NekoHub.Application.Abstractions.Security;
using NekoHub.Application.Auth;

namespace NekoHub.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var dataProtectionKeysPath = ResolveDataProtectionKeysPath(configuration, hostEnvironment.ContentRootPath);
        Directory.CreateDirectory(dataProtectionKeysPath);
        services
            .AddDataProtection()
            // AI provider 密钥保护和刷新令牌等敏感数据都依赖同一套 Data Protection key ring。
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

        services.AddSingleton<IAiProviderSecretProtector, DataProtectionAiProviderSecretProtector>();
        services
            .AddOptions<AssetApiOptions>()
            .Bind(configuration.GetSection(AssetApiOptions.SectionName));
        services.PostConfigure<AssetApiOptions>(options =>
        {
            options.AllowedContentTypes = AssetApiOptions.NormalizeAllowedContentTypes(options.AllowedContentTypes);
        });
        services.AddCors(options =>
        {
            options.AddPolicy(ApiCorsDefaults.PolicyName, policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        services
            .AddOptions<ApiKeyAuthOptions>()
            .Bind(configuration.GetSection(ApiKeyAuthOptions.SectionName))
            .Validate(
                static options => !options.Enabled || options.Keys.Any(static key => !string.IsNullOrWhiteSpace(key)),
                "Auth:ApiKey:Keys must contain at least one key when API key auth is enabled.")
            .ValidateOnStart();
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Secret), "Auth:Jwt:Secret is required.")
            .Validate(static options => options.AccessTokenMinutes > 0, "Auth:Jwt:AccessTokenMinutes must be greater than 0.")
            .Validate(static options => options.RefreshTokenDays > 0, "Auth:Jwt:RefreshTokenDays must be greater than 0.")
            .ValidateOnStart();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthorizationPolicies.HybridBearer;
                options.DefaultChallengeScheme = AuthorizationPolicies.HybridBearer;
            })
            .AddPolicyScheme(AuthorizationPolicies.HybridBearer, "JWT or API key", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    // 管理接口同时兼容 JWT 和 API key 时，先按请求头形态做一次轻量分流，
                    // 避免每个请求都去尝试两套认证方案。
                    var apiKeyEnabled = context.RequestServices
                        .GetRequiredService<IOptions<ApiKeyAuthOptions>>()
                        .Value
                        .Enabled;
                    if (!apiKeyEnabled)
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    if (!context.Request.Headers.TryGetValue("Authorization", out var rawAuthorizationHeader)
                        || string.IsNullOrWhiteSpace(rawAuthorizationHeader))
                    {
                        return ApiKeyAuthenticationDefaults.SchemeName;
                    }

                    if (!AuthenticationHeaderValue.TryParse(rawAuthorizationHeader.ToString(), out var headerValue)
                        || !string.Equals(headerValue.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrWhiteSpace(headerValue.Parameter))
                    {
                        return ApiKeyAuthenticationDefaults.SchemeName;
                    }

                    var token = headerValue.Parameter.Trim();
                    // JWT 在当前系统里固定是三段结构，不符合时直接按 API key 处理。
                    return token.Count(static character => character == '.') == 2
                        ? JwtBearerDefaults.AuthenticationScheme
                        : ApiKeyAuthenticationDefaults.SchemeName;
                };
            })
            .AddJwtBearer()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.SchemeName,
                _ => { });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;
                options.MapInboundClaims = true;
                options.TokenValidationParameters = JwtBearerEventsConfigurator.BuildTokenValidationParameters(jwtOptions);
                options.TokenValidationParameters.NameClaimType = ClaimTypes.Name;
                options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
                options.Events = JwtBearerEventsConfigurator.Build(Options.Create(jwtOptions));
            });

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.JwtUserRequired, policy =>
            {
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });

            options.AddPolicy(AuthorizationPolicies.ManagementAccess, policy =>
            {
                policy.AddAuthenticationSchemes(AuthorizationPolicies.HybridBearer);
                policy.RequireAuthenticatedUser();
            });

            options.AddPolicy(AuthorizationPolicies.ApiKeyOnly, policy =>
            {
                policy.AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.SchemeName);
                policy.RequireAuthenticatedUser();
            });

            foreach (var permission in PermissionCatalog.All)
            {
                options.AddPolicy(permission, policy =>
                {
                    policy.AddAuthenticationSchemes(AuthorizationPolicies.HybridBearer);
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new PermissionRequirement(permission));
                });
            }
        });
        services.AddSingleton<IMcpSessionManager, McpSessionManager>();
        services.AddScoped<IMcpServer, McpServer>();
        services.AddScoped<McpPromptRegistry>();
        services.AddScoped<IMcpPrompt, InspectAssetMcpPrompt>();
        services.AddScoped<IMcpPrompt, EnrichAssetMcpPrompt>();
        services.AddScoped<IMcpPrompt, ReviewAssetOutputsMcpPrompt>();
        services.AddScoped<McpResourceRegistry>();
        services.AddScoped<IMcpResource, AssetMcpResource>();
        services.AddScoped<IMcpResource, SkillMcpResource>();
        services.AddScoped<McpToolRegistry>();
        services.AddScoped<IMcpTool, GetAssetMcpTool>();
        services.AddScoped<IMcpTool, ListAssetsMcpTool>();
        services.AddScoped<IMcpTool, PatchAssetMcpTool>();
        services.AddScoped<IMcpTool, ListSkillsMcpTool>();
        services.AddScoped<IMcpTool, GetAssetContentUrlMcpTool>();
        services.AddScoped<IMcpTool, UploadAssetMcpTool>();
        services.AddScoped<IMcpTool, RunAssetSkillMcpTool>();
        services.AddScoped<IMcpTool, DeleteAssetMcpTool>();
        services.AddScoped<IMcpTool, BatchDeleteAssetsMcpTool>();
        services.AddScoped<IMcpTool, GetAssetUsageStatsMcpTool>();
        services.AddScoped<IMcpTool, ListStorageProfilesMcpTool>();
        services.AddScoped<IMcpTool, CreateStorageProfileMcpTool>();
        services.AddScoped<IMcpTool, UpdateStorageProfileMcpTool>();
        services.AddScoped<IMcpTool, DeleteStorageProfileMcpTool>();

        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                // PATCH 场景依赖 OptionalValue 保留“未提供 / 显式 null / 有值”三态语义。
                options.JsonSerializerOptions.Converters.Add(new OptionalValueJsonConverterFactory());
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var firstError = context.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();

                var response = new ApiErrorResponse(
                    new ApiError(
                        Code: "request_invalid",
                        Message: string.IsNullOrWhiteSpace(firstError) ? "Invalid request payload." : firstError,
                        TraceId: context.HttpContext.TraceIdentifier,
                        Status: StatusCodes.Status400BadRequest));

                return new BadRequestObjectResult(response);
            };
        });

        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "NekoHub API";
                document.Info.Version = "v1";
                document.Info.Description = "Agent-friendly media asset backend.";
                return Task.CompletedTask;
            });
        });

        return services;
    }

    private static string ResolveDataProtectionKeysPath(IConfiguration configuration, string contentRootPath)
    {
        var configuredPath = configuration["Security:DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(Path.Combine(contentRootPath, "storage", "keys"));
        }

        return Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }
}

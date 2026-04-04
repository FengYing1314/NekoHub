using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

namespace NekoHub.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataProtection();
        services.AddSingleton<IAiProviderSecretProtector, DataProtectionAiProviderSecretProtector>();
        services.Configure<AssetApiOptions>(configuration.GetSection(AssetApiOptions.SectionName));
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
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ApiKeyAuthenticationDefaults.SchemeName;
                options.DefaultChallengeScheme = ApiKeyAuthenticationDefaults.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.SchemeName,
                _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ApiKeyAuthorization.PolicyName, policy =>
            {
                policy.AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.SchemeName);
                policy.RequireAuthenticatedUser();
            });
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
}

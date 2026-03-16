using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.Configuration;

namespace NekoHub.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AssetApiOptions>(configuration.GetSection(AssetApiOptions.SectionName));

        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
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

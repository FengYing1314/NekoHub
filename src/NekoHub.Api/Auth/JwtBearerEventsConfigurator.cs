using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NekoHub.Api.Configuration;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Abstractions.Persistence;

namespace NekoHub.Api.Auth;

public static class JwtBearerEventsConfigurator
{
    public static JwtBearerEvents Build(IOptions<JwtOptions> jwtOptions)
    {
        return new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                var userId = PrincipalClaimReader.GetUserId(context.Principal);
                if (userId is null)
                {
                    context.Fail("JWT token is missing a valid user identifier.");
                    return;
                }

                var user = await userRepository.GetByIdAsync(userId.Value, context.HttpContext.RequestAborted);
                if (user is null || !user.IsActive)
                {
                    context.Fail("JWT token belongs to an inactive or missing user.");
                }
            },
            OnChallenge = async context =>
            {
                if (context.Response.HasStarted)
                {
                    return;
                }

                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var response = new ApiErrorResponse(
                    new ApiError(
                        Code: "auth_unauthorized",
                        Message: "Authentication is required.",
                        TraceId: context.HttpContext.TraceIdentifier,
                        Status: StatusCodes.Status401Unauthorized));

                await context.Response.WriteAsJsonAsync(response);
            }
        };
    }

    public static TokenValidationParameters BuildTokenValidationParameters(JwtOptions options)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(options.Secret)),
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }
}

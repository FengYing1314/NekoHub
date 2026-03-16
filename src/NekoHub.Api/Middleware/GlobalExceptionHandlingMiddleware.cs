using System.Net;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Api.Middleware;

public sealed class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", context.TraceIdentifier);
            await WriteErrorResponseAsync(context, exception);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;
        var code = "internal_server_error";
        var message = "An unexpected error occurred.";

        if (exception is AppException appException)
        {
            statusCode = (HttpStatusCode)appException.StatusCode;
            code = appException.Code;
            message = appException.Message;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new ApiErrorResponse(
            new ApiError(
                Code: code,
                Message: message,
                TraceId: context.TraceIdentifier,
                Status: (int)statusCode));

        await context.Response.WriteAsJsonAsync(response);
    }
}

using System.Net;

namespace NekoHub.Application.Abstractions.Ai;

public interface IAiVisionClient
{
    Task<AiVisionResponse> GenerateAsync(
        AiVisionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AiVisionRequest(
    string ApiBaseUrl,
    string ApiKey,
    string ModelName,
    string SystemPrompt,
    string UserPrompt,
    string ImageDataUrl);

public sealed record AiVisionResponse(
    string ModelName,
    string Caption);

public sealed class AiVisionException : Exception
{
    public AiVisionException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}

using System.Net;

namespace NekoHub.Infrastructure.Ai;

public interface IOpenAiVisionClient
{
    Task<OpenAiVisionResponse> GenerateAsync(
        OpenAiVisionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record OpenAiVisionRequest(
    string ApiBaseUrl,
    string ApiKey,
    string ModelName,
    string SystemPrompt,
    string UserPrompt,
    string ImageDataUrl);

public sealed record OpenAiVisionResponse(
    string ModelName,
    string Caption);

public sealed class OpenAiVisionException : Exception
{
    public OpenAiVisionException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}

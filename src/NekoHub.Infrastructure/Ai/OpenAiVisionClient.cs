using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NekoHub.Infrastructure.Ai;

public sealed class OpenAiVisionClient(HttpClient httpClient) : IOpenAiVisionClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<OpenAiVisionResponse> GenerateAsync(
        OpenAiVisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ResolveChatCompletionsEndpoint(request.ApiBaseUrl));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey.Trim());
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = JsonContent.Create(new
        {
            model = request.ModelName.Trim(),
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = request.SystemPrompt.Trim()
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = request.UserPrompt.Trim()
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = request.ImageDataUrl.Trim()
                            }
                        }
                    }
                }
            }
        }, options: SerializerOptions);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OpenAiVisionException("OpenAI vision request timed out.", null, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new OpenAiVisionException("OpenAI vision request failed to reach the remote endpoint.", null, exception);
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new OpenAiVisionException(
                    BuildApiErrorMessage(response.StatusCode, responseBody),
                    response.StatusCode);
            }

            ChatCompletionsResponse? payload;
            try
            {
                payload = JsonSerializer.Deserialize<ChatCompletionsResponse>(responseBody, SerializerOptions);
            }
            catch (JsonException exception)
            {
                throw new OpenAiVisionException("OpenAI vision response payload was not valid JSON.", response.StatusCode, exception);
            }

            var caption = payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            if (string.IsNullOrWhiteSpace(caption))
            {
                throw new OpenAiVisionException("OpenAI vision response did not contain caption text.", response.StatusCode);
            }

            return new OpenAiVisionResponse(
                ModelName: string.IsNullOrWhiteSpace(payload?.Model) ? request.ModelName.Trim() : payload.Model.Trim(),
                Caption: caption);
        }
    }

    private static void ValidateRequest(OpenAiVisionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiBaseUrl))
        {
            throw new ArgumentException("ApiBaseUrl is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new ArgumentException("ApiKey is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ModelName))
        {
            throw new ArgumentException("ModelName is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            throw new ArgumentException("SystemPrompt is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            throw new ArgumentException("UserPrompt is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ImageDataUrl))
        {
            throw new ArgumentException("ImageDataUrl is required.", nameof(request));
        }
    }

    private static Uri ResolveChatCompletionsEndpoint(string apiBaseUrl)
    {
        var normalizedBaseUrl = apiBaseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new ArgumentException("ApiBaseUrl must be an absolute URL.", nameof(apiBaseUrl));
        }

        var relativePath = baseUri.AbsolutePath.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? "chat/completions"
            : "v1/chat/completions";

        return new Uri($"{normalizedBaseUrl}/{relativePath}", UriKind.Absolute);
    }

    private static string BuildApiErrorMessage(HttpStatusCode statusCode, string responseBody)
    {
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<ApiErrorEnvelope>(responseBody, SerializerOptions);
                if (!string.IsNullOrWhiteSpace(payload?.Error?.Message))
                {
                    return $"OpenAI vision request failed with status {(int)statusCode}: {payload.Error.Message}";
                }
            }
            catch (JsonException)
            {
            }
        }

        return $"OpenAI vision request failed with status {(int)statusCode}.";
    }

    private sealed record ChatCompletionsResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionsChoice>? Choices);

    private sealed record ChatCompletionsChoice(
        [property: JsonPropertyName("message")] ChatCompletionsMessage? Message);

    private sealed record ChatCompletionsMessage(
        [property: JsonPropertyName("content")] string? Content);

    private sealed record ApiErrorEnvelope(
        [property: JsonPropertyName("error")] ApiErrorDetails? Error);

    private sealed record ApiErrorDetails(
        [property: JsonPropertyName("message")] string? Message);
}

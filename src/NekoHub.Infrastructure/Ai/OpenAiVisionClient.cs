using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NekoHub.Application.Abstractions.Ai;

namespace NekoHub.Infrastructure.Ai;

public sealed class OpenAiVisionClient(HttpClient httpClient) : IAiVisionClient
{
    private const string EmptyAssistantContentMessage =
        "Provider returned 200 OK but no assistant content in standard chat completion response.";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AiVisionResponse> GenerateAsync(
        AiVisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        using var response = await SendChatCompletionsRequestAsync(request, stream: false, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiVisionException(
                BuildApiErrorMessage(response.StatusCode, responseBody),
                response.StatusCode);
        }

        using var payload = DeserializeChatCompletionsPayload(responseBody, response.StatusCode);
        var resolvedModelName = ResolveModelName(payload.RootElement, request.ModelName);
        var standardContent = TryExtractAssistantMessageContent(payload.RootElement);
        if (!string.IsNullOrWhiteSpace(standardContent))
        {
            return new AiVisionResponse(
                ModelName: resolvedModelName,
                Caption: ExtractCaption(standardContent, response.StatusCode));
        }

        return await GenerateWithStreamingFallbackAsync(request, resolvedModelName, cancellationToken);
    }

    private static void ValidateRequest(AiVisionRequest request)
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

    private async Task<AiVisionResponse> GenerateWithStreamingFallbackAsync(
        AiVisionRequest request,
        string resolvedModelName,
        CancellationToken cancellationToken)
    {
        using var response = await SendChatCompletionsRequestAsync(request, stream: true, cancellationToken);
        var fallbackStatusCode = response.StatusCode;
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AiVisionException(
                $"{EmptyAssistantContentMessage} Streaming fallback failed. {BuildApiErrorMessage(fallbackStatusCode, errorBody)}",
                fallbackStatusCode);
        }

        if (IsJsonContentType(response.Content.Headers.ContentType?.MediaType))
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            using var payload = DeserializeChatCompletionsPayload(responseBody, fallbackStatusCode);
            var fallbackModelName = ResolveModelName(payload.RootElement, resolvedModelName);
            var content = TryExtractAssistantMessageContent(payload.RootElement);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AiVisionException(
                    $"{EmptyAssistantContentMessage} Streaming fallback returned 200 OK but no assistant content.",
                    fallbackStatusCode);
            }

            return new AiVisionResponse(
                ModelName: fallbackModelName,
                Caption: ExtractCaption(content, fallbackStatusCode));
        }

        return await ParseStreamingFallbackAsync(
            response,
            resolvedModelName,
            cancellationToken);
    }

    private async Task<AiVisionResponse> ParseStreamingFallbackAsync(
        HttpResponseMessage response,
        string resolvedModelName,
        CancellationToken cancellationToken)
    {
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream, Encoding.UTF8);
        var aggregatedContent = new StringBuilder();
        var sawSseEvent = false;
        var fallbackModelName = resolvedModelName;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            sawSseEvent = true;
            var data = line["data:".Length..].Trim();
            if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
            {
                break;
            }

            using var payload = DeserializeChatCompletionsPayload(data, response.StatusCode);
            fallbackModelName = ResolveModelName(payload.RootElement, fallbackModelName);
            AppendStreamingDeltaContent(payload.RootElement, aggregatedContent);
        }

        if (aggregatedContent.Length == 0)
        {
            throw new AiVisionException(
                sawSseEvent
                    ? $"{EmptyAssistantContentMessage} Streaming fallback also did not yield caption content."
                    : $"{EmptyAssistantContentMessage} Streaming fallback did not return server-sent events.",
                response.StatusCode);
        }

        return new AiVisionResponse(
            ModelName: fallbackModelName,
            Caption: ExtractCaption(aggregatedContent.ToString(), response.StatusCode));
    }

    private async Task<HttpResponseMessage> SendChatCompletionsRequestAsync(
        AiVisionRequest request,
        bool stream,
        CancellationToken cancellationToken)
    {
        using var httpRequest = CreateChatCompletionsRequest(request, stream);

        try
        {
            return await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiVisionException("OpenAI vision request timed out.", null, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AiVisionException("OpenAI vision request failed to reach the remote endpoint.", null, exception);
        }
    }

    private HttpRequestMessage CreateChatCompletionsRequest(AiVisionRequest request, bool stream)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, ResolveChatCompletionsEndpoint(request.ApiBaseUrl));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey.Trim());
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (stream)
        {
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        }

        httpRequest.Content = JsonContent.Create(new
        {
            model = request.ModelName.Trim(),
            response_format = new
            {
                type = "json_object"
            },
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
            },
            stream = stream ? true : (bool?)null
        }, options: SerializerOptions);

        return httpRequest;
    }

    private static JsonDocument DeserializeChatCompletionsPayload(string responseBody, HttpStatusCode? statusCode)
    {
        try
        {
            return JsonDocument.Parse(responseBody);
        }
        catch (JsonException exception)
        {
            throw new AiVisionException("OpenAI vision response payload was not valid JSON.", statusCode, exception);
        }
    }

    private static string ResolveModelName(JsonElement payload, string fallbackModelName)
    {
        var modelName = TryGetString(payload, "model");
        return string.IsNullOrWhiteSpace(modelName)
            ? fallbackModelName.Trim()
            : modelName.Trim();
    }

    private static string? TryExtractAssistantMessageContent(JsonElement payload)
    {
        if (!payload.TryGetProperty("choices", out var choices)
            || choices.ValueKind is not JsonValueKind.Array)
        {
            return null;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (!choice.TryGetProperty("message", out var message)
                || message.ValueKind is not JsonValueKind.Object
                || !message.TryGetProperty("content", out var content))
            {
                continue;
            }

            var extracted = ExtractTextContent(content);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                return extracted.Trim();
            }
        }

        return null;
    }

    private static void AppendStreamingDeltaContent(JsonElement payload, StringBuilder buffer)
    {
        if (!payload.TryGetProperty("choices", out var choices)
            || choices.ValueKind is not JsonValueKind.Array)
        {
            return;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (!choice.TryGetProperty("delta", out var delta)
                || delta.ValueKind is not JsonValueKind.Object
                || !delta.TryGetProperty("content", out var content))
            {
                continue;
            }

            var extracted = ExtractTextContent(content);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                buffer.Append(extracted);
            }
        }
    }

    private static string? ExtractTextContent(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => NormalizeExtractedText(content.GetString()),
            JsonValueKind.Array => ExtractTextFromArray(content),
            JsonValueKind.Object => ExtractTextFromObject(content),
            _ => null
        };
    }

    private static string? ExtractTextFromArray(JsonElement content)
    {
        var buffer = new StringBuilder();
        foreach (var item in content.EnumerateArray())
        {
            var extracted = item.ValueKind switch
            {
                JsonValueKind.String => NormalizeExtractedText(item.GetString()),
                JsonValueKind.Object => ExtractTextFromObject(item),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(extracted))
            {
                buffer.Append(extracted);
            }
        }

        return NormalizeExtractedText(buffer.ToString());
    }

    private static string? ExtractTextFromObject(JsonElement content)
    {
        if (TryGetString(content, "text") is { } directText)
        {
            return NormalizeExtractedText(directText);
        }

        if (content.TryGetProperty("text", out var textNode)
            && textNode.ValueKind is JsonValueKind.Object
            && TryGetString(textNode, "value") is { } textValue)
        {
            return NormalizeExtractedText(textValue);
        }

        if (content.TryGetProperty("content", out var nestedContent))
        {
            return ExtractTextContent(nestedContent);
        }

        return null;
    }

    private static string? NormalizeExtractedText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private static bool IsJsonContentType(string? mediaType)
    {
        return !string.IsNullOrWhiteSpace(mediaType)
               && mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
               && property.ValueKind is JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string ExtractCaption(string content, HttpStatusCode? statusCode)
    {
        CaptionPayload? captionPayload;
        try
        {
            captionPayload = JsonSerializer.Deserialize<CaptionPayload>(content.Trim(), SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new AiVisionException("OpenAI vision response content was not valid JSON.", statusCode, exception);
        }

        var caption = captionPayload?.Caption?.Trim();
        if (string.IsNullOrWhiteSpace(caption))
        {
            throw new AiVisionException("OpenAI vision response JSON did not contain a caption field.", statusCode);
        }

        return caption;
    }

    private sealed record CaptionPayload(
        [property: JsonPropertyName("caption")] string? Caption);

    private sealed record ApiErrorEnvelope(
        [property: JsonPropertyName("error")] ApiErrorDetails? Error);

    private sealed record ApiErrorDetails(
        [property: JsonPropertyName("message")] string? Message);
}

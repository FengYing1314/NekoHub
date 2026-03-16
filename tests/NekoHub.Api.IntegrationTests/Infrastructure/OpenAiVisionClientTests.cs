using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NekoHub.Application.Abstractions.Ai;
using NekoHub.Infrastructure.Ai;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class OpenAiVisionClientTests
{
    [Fact]
    public async Task GenerateAsync_Should_Send_ResponseFormat_And_Parse_Caption_From_Json_Content()
    {
        JsonDocument? requestPayload = null;

        using var handler = new DelegatingStubHttpMessageHandler(async (request, cancellationToken) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.AbsoluteUri.Should().Be("https://api.example.com/v1/chat/completions");
            request.Headers.Authorization.Should().BeEquivalentTo(new AuthenticationHeaderValue("Bearer", "sk-secret"));

            requestPayload = await JsonDocument.ParseAsync(
                await request.Content!.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            var payload = new
            {
                model = "gpt-4o",
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = "{\"caption\":\"A black cat sitting on a windowsill.\"}"
                        }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload)
            };
        });

        using var httpClient = CreateHttpClient(handler);
        var client = new OpenAiVisionClient(httpClient);

        var response = await client.GenerateAsync(new AiVisionRequest(
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "sk-secret",
            ModelName: "gpt-4o",
            SystemPrompt: "Return JSON only.",
            UserPrompt: "Analyze this image.",
            ImageDataUrl: "data:image/png;base64,AAA"));

        response.ModelName.Should().Be("gpt-4o");
        response.Caption.Should().Be("A black cat sitting on a windowsill.");

        requestPayload.Should().NotBeNull();
        requestPayload!.RootElement.GetProperty("response_format").GetProperty("type").GetString().Should().Be("json_object");
        requestPayload.RootElement.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("system");
        requestPayload.RootElement.TryGetProperty("stream", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_Should_Parse_Caption_From_Content_Block_Array()
    {
        var requestCount = 0;

        using var handler = new DelegatingStubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            var payload = new
            {
                model = "gpt-4o-mini",
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = new object[]
                            {
                                new
                                {
                                    type = "text",
                                    text = "{\"caption\":\"A calico cat looking out a window.\"}"
                                }
                            }
                        }
                    }
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload)
            });
        });

        using var httpClient = CreateHttpClient(handler);
        var client = new OpenAiVisionClient(httpClient);

        var response = await client.GenerateAsync(new AiVisionRequest(
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "sk-secret",
            ModelName: "gpt-4o-mini",
            SystemPrompt: "Return JSON only.",
            UserPrompt: "Analyze this image.",
            ImageDataUrl: "data:image/png;base64,AAA"));

        requestCount.Should().Be(1);
        response.Caption.Should().Be("A calico cat looking out a window.");
    }

    [Fact]
    public async Task GenerateAsync_When_Standard_Response_Has_No_Content_Should_Fallback_To_Streaming()
    {
        var requestPayloads = new List<JsonDocument>();
        var requestCount = 0;

        using var handler = new DelegatingStubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestCount++;
            requestPayloads.Add(await JsonDocument.ParseAsync(
                await request.Content!.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken));

            if (requestCount == 1)
            {
                var emptyPayload = new
                {
                    model = "gpt-5.4",
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                content = (string?)null
                            }
                        }
                    }
                };

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(emptyPayload)
                };
            }

            return CreateStreamingResponse(
                """{"model":"gpt-5.4","choices":[{"delta":{"reasoning_content":"Thinking about the image"}}]}""",
                """{"model":"gpt-5.4","choices":[{"delta":{"content":"{\"caption\":\""}}]}""",
                """{"model":"gpt-5.4","choices":[{"delta":{"content":"Recovered caption"}}]}""",
                """{"model":"gpt-5.4","choices":[{"delta":{"content":"\"}"}}]}"""
            );
        });

        using var httpClient = CreateHttpClient(handler);
        var client = new OpenAiVisionClient(httpClient);

        var response = await client.GenerateAsync(new AiVisionRequest(
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "sk-secret",
            ModelName: "gpt-5.4",
            SystemPrompt: "Return JSON only.",
            UserPrompt: "Analyze this image.",
            ImageDataUrl: "data:image/png;base64,AAA"));

        requestCount.Should().Be(2);
        requestPayloads[0].RootElement.TryGetProperty("stream", out _).Should().BeFalse();
        requestPayloads[1].RootElement.GetProperty("stream").GetBoolean().Should().BeTrue();
        response.ModelName.Should().Be("gpt-5.4");
        response.Caption.Should().Be("Recovered caption");
    }

    [Fact]
    public async Task GenerateAsync_Should_Throw_When_Response_Content_Is_Not_Valid_Json_Object()
    {
        using var handler = new DelegatingStubHttpMessageHandler((_, _) =>
        {
            var payload = new
            {
                model = "gpt-4o",
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = "plain text caption"
                        }
                    }
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload)
            });
        });

        using var httpClient = CreateHttpClient(handler);
        var client = new OpenAiVisionClient(httpClient);

        var act = () => client.GenerateAsync(new AiVisionRequest(
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "sk-secret",
            ModelName: "gpt-4o",
            SystemPrompt: "Return JSON only.",
            UserPrompt: "Analyze this image.",
            ImageDataUrl: "data:image/png;base64,AAA"));

        await act.Should()
            .ThrowAsync<AiVisionException>()
            .WithMessage("OpenAI vision response content was not valid JSON.");
    }

    [Fact]
    public async Task GenerateAsync_When_Streaming_Fallback_Has_No_Content_Should_Throw_Readable_Error()
    {
        var requestCount = 0;

        using var handler = new DelegatingStubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                var emptyPayload = new
                {
                    model = "gpt-5.4",
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                content = (string?)null
                            }
                        }
                    }
                };

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(emptyPayload)
                });
            }

            return Task.FromResult(CreateStreamingResponse(
                """{"model":"gpt-5.4","choices":[{"delta":{"reasoning_content":"Still thinking"}}]}"""
            ));
        });

        using var httpClient = CreateHttpClient(handler);
        var client = new OpenAiVisionClient(httpClient);

        var act = () => client.GenerateAsync(new AiVisionRequest(
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "sk-secret",
            ModelName: "gpt-5.4",
            SystemPrompt: "Return JSON only.",
            UserPrompt: "Analyze this image.",
            ImageDataUrl: "data:image/png;base64,AAA"));

        await act.Should()
            .ThrowAsync<AiVisionException>()
            .WithMessage("*Streaming fallback also did not yield caption content.*");
    }

    [Fact]
    public async Task GenerateAsync_When_Streaming_Fallback_Content_Is_Not_Valid_Json_Should_Keep_InvalidJson_Error()
    {
        var requestCount = 0;

        using var handler = new DelegatingStubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                var emptyPayload = new
                {
                    model = "gpt-5.4",
                    choices = new[]
                    {
                        new
                        {
                            message = new
                            {
                                content = (string?)null
                            }
                        }
                    }
                };

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(emptyPayload)
                });
            }

            return Task.FromResult(CreateStreamingResponse(
                """{"model":"gpt-5.4","choices":[{"delta":{"content":"plain text caption"}}]}"""
            ));
        });

        using var httpClient = CreateHttpClient(handler);
        var client = new OpenAiVisionClient(httpClient);

        var act = () => client.GenerateAsync(new AiVisionRequest(
            ApiBaseUrl: "https://api.example.com/v1",
            ApiKey: "sk-secret",
            ModelName: "gpt-5.4",
            SystemPrompt: "Return JSON only.",
            UserPrompt: "Analyze this image.",
            ImageDataUrl: "data:image/png;base64,AAA"));

        await act.Should()
            .ThrowAsync<AiVisionException>()
            .WithMessage("OpenAI vision response content was not valid JSON.");
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    private static HttpResponseMessage CreateStreamingResponse(params string[] payloads)
    {
        var builder = new StringBuilder();
        foreach (var payload in payloads)
        {
            builder.Append("data: ");
            builder.Append(payload);
            builder.Append("\n\n");
        }

        builder.Append("data: [DONE]\n\n");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(builder.ToString(), Encoding.UTF8, "text/event-stream")
        };
    }

    private sealed class DelegatingStubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}

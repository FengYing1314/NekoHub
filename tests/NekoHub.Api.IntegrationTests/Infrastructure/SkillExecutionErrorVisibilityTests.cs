using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NekoHub.Application.Abstractions.Ai;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class SkillExecutionErrorVisibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Upload_When_Basic_Caption_Fails_Should_Persist_Readable_Step_Error_Message()
    {
        using var factory = new FailingAiVisionApplicationFactory();
        factory.EnsureTestingAiRuntime();
        using var client = factory.CreateClient();

        var assetId = await UploadTestPngAsync(client, "skill-error-visibility.png");

        var detail = await EventuallyAsync(async () =>
        {
            var response = await client.GetAsync($"/api/v1/assets/{assetId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await GetResponseDataAsync<AssetResponse>(response);
        }, item => item?.LatestExecutionSummary?.Steps.Any(step => step.StepName == "generate_basic_caption") == true);

        detail.Should().NotBeNull();
        var captionStep = detail!.LatestExecutionSummary!.Steps.Single(step => step.StepName == "generate_basic_caption");
        captionStep.Succeeded.Should().BeFalse();
        captionStep.ErrorMessage.Should().Contain("caption field");
        captionStep.ErrorMessage.Should().NotBe("Step 'generate_basic_caption' failed.");
    }

    private static async Task<Guid> UploadTestPngAsync(HttpClient client, string fileName)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreatePngBytes(2, 2));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        requestContent.Add(fileContent, "File", fileName);

        var response = await client.PostAsync("/api/v1/assets", requestContent);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asset = await GetResponseDataAsync<AssetResponse>(response);
        asset.Should().NotBeNull();
        return asset!.Id;
    }

    private static async Task<T> EventuallyAsync<T>(
        Func<Task<T>> action,
        Func<T, bool> predicate,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);
        var effectivePollInterval = pollInterval ?? TimeSpan.FromMilliseconds(200);
        var deadline = DateTimeOffset.UtcNow.Add(effectiveTimeout);
        Exception? lastException = null;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            try
            {
                var result = await action();
                if (predicate(result))
                {
                    return result;
                }
            }
            catch (Exception exception)
            {
                lastException = exception;
            }

            await Task.Delay(effectivePollInterval);
        }

        throw new TimeoutException(
            "The expected condition was not satisfied within the polling timeout.",
            lastException);
    }

    private static async Task<T?> GetResponseDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions);
        return payload != null ? payload.Data : default;
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(80, 140, 255, 255));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }

    private sealed class FailingAiVisionApplicationFactory : NekoHubApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAiVisionClient>();
                services.AddSingleton<IAiVisionClient>(new FailingAiVisionClient());
            });
        }
    }

    private sealed class FailingAiVisionClient : IAiVisionClient
    {
        public Task<AiVisionResponse> GenerateAsync(
            AiVisionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new AiVisionException("OpenAI vision response JSON did not contain a caption field.");
        }
    }
}

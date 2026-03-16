using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public abstract class IntegrationTestBase : IClassFixture<NekoHubApplicationFactory>
{
    protected readonly HttpClient Client;
    protected readonly NekoHubApplicationFactory Factory;
    protected readonly JsonSerializerOptions JsonOptions;

    protected IntegrationTestBase(NekoHubApplicationFactory factory)
    {
        Factory = factory;
        // 默认测试走已认证客户端，只有显式关闭 AutoAuthenticateClient 的 factory 才使用匿名入口。
        Client = factory.AutoAuthenticateClient
            ? factory.CreateAuthenticatedClientAsync().GetAwaiter().GetResult()
            : factory.CreateClient();
        // 统一提前补齐测试 AI runtime，避免各用例重复关心 caption 类依赖的 provider 前置条件。
        factory.EnsureTestingAiRuntime();
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    protected Task<HttpClient> CreateAuthenticatedClientAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions? options = null)
    {
        return Factory.CreateAuthenticatedClientAsync(options);
    }

    protected HttpClient CreateAnonymousClient(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions? options = null)
    {
        return Factory.CreateAnonymousClient(options);
    }

    protected async Task<T?> GetResponseDataAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions);
        return content != null ? content.Data : default;
    }

    protected async Task<ApiError?> GetErrorAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);
        return content?.Error;
    }

    protected async Task<T> EventuallyAsync<T>(
        Func<Task<T>> action,
        Func<T, bool> predicate,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        // 上传后异步工作流、队列消费和轮询读取都会用到 EventuallyAsync，测试里统一采用同一套轮询语义。
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
}

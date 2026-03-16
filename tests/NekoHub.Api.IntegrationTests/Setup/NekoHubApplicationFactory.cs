using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Application.Abstractions.Ai;
using NekoHub.Application.Abstractions.Security;
using NekoHub.Domain.Ai;
using NekoHub.Infrastructure.Ai;
using NekoHub.Infrastructure.Persistence;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NekoHub.Api.IntegrationTests.Setup;

public class NekoHubApplicationFactory : WebApplicationFactory<Program>
{
    public const string BootstrapAdminUsername = "superadmin";
    public const string BootstrapAdminPassword = "superadmin-pass-123";
    private const string TestJwtSecret = "testing-jwt-secret-at-least-32-chars";
    private static readonly Uri TestBaseAddress = new("https://localhost");

    // 每个 factory 使用独立临时存储目录，避免并行测试之间共享文件状态。
    public string TestStoragePath { get; } = Path.Combine(Path.GetTempPath(), "NekoHubTests", Guid.NewGuid().ToString());
    public virtual bool AutoAuthenticateClient => true;
    // 数据库 lease 与 factory 生命周期绑定，确保同一组测试共享应用实例但不共享外部数据库名称。
    private readonly PostgresTestDatabaseLease _databaseLease = PostgresTestEnvironment.CreateDatabaseLease("nekohub_it");
    private readonly object _aiSeedLock = new();
    private bool _testingAiRuntimeSeeded;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(CreateInMemoryConfiguration());
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAiVisionClient>();
            // 集成测试保留完整业务链路，但把外部 AI 调用替换成稳定 stub，避免网络和配额依赖。
            services.AddSingleton<IAiVisionClient, StubOpenAiVisionClient>();
        });
    }

    protected virtual IDictionary<string, string?> CreateInMemoryConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["Auth:ApiKey:Enabled"] = "false",
            ["Auth:Jwt:Issuer"] = "NekoHub.Testing",
            ["Auth:Jwt:Audience"] = "NekoHub.Testing.Admin",
            ["Auth:Jwt:Secret"] = TestJwtSecret,
            ["Auth:Jwt:AccessTokenMinutes"] = "15",
            ["Auth:Jwt:RefreshTokenDays"] = "30",
            ["Auth:BootstrapSuperAdmin:Username"] = BootstrapAdminUsername,
            ["Auth:BootstrapSuperAdmin:Password"] = BootstrapAdminPassword,
            ["Persistence:Database:Provider"] = "postgresql",
            ["Persistence:Database:ConnectionString"] = _databaseLease.ConnectionString,
            ["Storage:Provider"] = "local",
            ["Storage:Local:RootPath"] = TestStoragePath,
            ["Storage:PublicBaseUrl"] = "http://test-server/content"
        };
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        WebApplicationFactoryClientOptions? options = null)
    {
        // 通过真实登录接口换取 token，而不是伪造 claims，保证认证/刷新链路也在测试覆盖内。
        var client = base.CreateClient(CreateDefaultClientOptions(options));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = BootstrapAdminUsername,
            password = BootstrapAdminPassword
        });

        loginResponse.EnsureSuccessStatusCode();
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        });

        if (authResponse?.Data?.AccessToken is null)
        {
            throw new InvalidOperationException("Failed to acquire bootstrap admin access token for tests.");
        }

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Data.AccessToken);
        return client;
    }

    public HttpClient CreateAnonymousClient(WebApplicationFactoryClientOptions? options = null)
    {
        return base.CreateClient(CreateDefaultClientOptions(options));
    }

    public new HttpClient CreateClient()
    {
        return AutoAuthenticateClient
            ? CreateAuthenticatedClientAsync().GetAwaiter().GetResult()
            : base.CreateClient(CreateDefaultClientOptions());
    }

    public new HttpClient CreateClient(WebApplicationFactoryClientOptions options)
    {
        return AutoAuthenticateClient
            ? CreateAuthenticatedClientAsync(options).GetAwaiter().GetResult()
            : base.CreateClient(CreateDefaultClientOptions(options));
    }

    public void EnsureTestingAiRuntime()
    {
        if (_testingAiRuntimeSeeded)
        {
            return;
        }

        lock (_aiSeedLock)
        {
            if (_testingAiRuntimeSeeded)
            {
                return;
            }

            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
            var secretProtector = scope.ServiceProvider.GetRequiredService<IAiProviderSecretProtector>();
            if (!dbContext.AiProviderProfiles.Any(profile => profile.IsActive))
            {
                // 只在缺少活动 provider 时补一条最小可用记录，供 caption/enrich 测试走完整运行时路径。
                const string apiKey = "testing-api-key";
                dbContext.AiProviderProfiles.Add(new AiProviderProfile(
                    id: Guid.CreateVersion7(),
                    name: "testing-ai",
                    apiBaseUrl: "https://testing.example.com/v1",
                    apiKey: secretProtector.Protect(apiKey),
                    apiKeyMasked: "test***",
                    modelName: "stub.basic_caption.v1",
                    defaultSystemPrompt: "Testing system prompt.",
                    isActive: true));
                dbContext.SaveChanges();
            }

            _testingAiRuntimeSeeded = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _databaseLease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (Directory.Exists(TestStoragePath))
            {
                // 清理失败不反向污染测试结果，目录残留由系统临时目录策略兜底。
                try { Directory.Delete(TestStoragePath, true); } catch { }
            }
        }
    }

    private sealed class StubOpenAiVisionClient : IAiVisionClient
    {
        public Task<AiVisionResponse> GenerateAsync(
            AiVisionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiVisionResponse(
                ModelName: "stub.basic_caption.v1",
                Caption: "stub caption"));
        }
    }

    private static WebApplicationFactoryClientOptions CreateDefaultClientOptions(
        WebApplicationFactoryClientOptions? options = null)
    {
        return new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = options?.AllowAutoRedirect ?? true,
            BaseAddress = options?.BaseAddress ?? TestBaseAddress,
            HandleCookies = options?.HandleCookies ?? true
        };
    }
}

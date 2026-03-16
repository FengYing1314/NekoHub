namespace NekoHub.Api.IntegrationTests.Setup;

public sealed class NekoHubApiKeyApplicationFactory : NekoHubApplicationFactory
{
    public const string TestApiKey = "nekohub-test-api-key";
    // API key 场景下不自动登录，交由具体测试自行决定请求头。
    public override bool AutoAuthenticateClient => false;

    protected override IDictionary<string, string?> CreateInMemoryConfiguration()
    {
        var configuration = base.CreateInMemoryConfiguration();
        configuration["Auth:ApiKey:Enabled"] = "true";
        configuration["Auth:ApiKey:Keys:0"] = TestApiKey;
        return configuration;
    }
}

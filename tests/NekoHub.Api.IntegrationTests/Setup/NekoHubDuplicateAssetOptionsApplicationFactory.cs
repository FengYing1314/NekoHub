namespace NekoHub.Api.IntegrationTests.Setup;

public sealed class NekoHubDuplicateAssetOptionsApplicationFactory : NekoHubApplicationFactory
{
    public override bool AutoAuthenticateClient => false;

    protected override IDictionary<string, string?> CreateInMemoryConfiguration()
    {
        var configuration = base.CreateInMemoryConfiguration();
        configuration["Api:Assets:AllowedContentTypes:4"] = " image/png ";
        configuration["Api:Assets:AllowedContentTypes:5"] = "IMAGE/WEBP";
        configuration["Api:Assets:AllowedContentTypes:6"] = " ";
        return configuration;
    }
}

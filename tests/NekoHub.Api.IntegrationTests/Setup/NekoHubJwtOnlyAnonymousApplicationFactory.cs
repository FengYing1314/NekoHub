namespace NekoHub.Api.IntegrationTests.Setup;

public sealed class NekoHubJwtOnlyAnonymousApplicationFactory : NekoHubApplicationFactory
{
    public override bool AutoAuthenticateClient => false;
}

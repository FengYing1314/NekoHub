namespace NekoHub.Api.Configuration;

public sealed class ApiKeyAuthOptions
{
    public const string SectionName = "Auth:ApiKey";

    public bool Enabled { get; init; } = false;

    public string[] Keys { get; init; } = [];
}

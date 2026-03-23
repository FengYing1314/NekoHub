namespace NekoHub.Api.Configuration;

public sealed class ApiKeyAuthOptions
{
    public const string SectionName = "Auth:ApiKey";

    public bool Enabled { get; init; } = true;

    public string[] Keys { get; init; } = [];
}

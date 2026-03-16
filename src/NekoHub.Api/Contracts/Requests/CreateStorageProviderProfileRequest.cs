using System.Text.Json;

namespace NekoHub.Api.Contracts.Requests;

public sealed class CreateStorageProviderProfileRequest
{
    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    public string? ProviderType { get; init; }

    public bool? IsEnabled { get; init; }

    public bool? IsDefault { get; init; }

    public JsonElement? Configuration { get; init; }

    public JsonElement? SecretConfiguration { get; init; }
}

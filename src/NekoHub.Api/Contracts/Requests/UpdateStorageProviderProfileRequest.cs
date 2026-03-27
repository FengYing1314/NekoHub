using System.Text.Json;
using NekoHub.Application.Common.Models;

namespace NekoHub.Api.Contracts.Requests;

public sealed class UpdateStorageProviderProfileRequest
{
    public OptionalValue<string?> Name { get; init; }

    public OptionalValue<string?> DisplayName { get; init; }

    public OptionalValue<bool> IsEnabled { get; init; }

    public OptionalValue<JsonElement?> Configuration { get; init; }

    public OptionalValue<JsonElement?> SecretConfiguration { get; init; }
}

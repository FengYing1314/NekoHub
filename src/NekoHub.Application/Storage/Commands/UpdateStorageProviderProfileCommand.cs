using System.Text.Json;
using NekoHub.Application.Common.Models;

namespace NekoHub.Application.Storage.Commands;

public sealed record UpdateStorageProviderProfileCommand(
    Guid ProfileId,
    OptionalValue<string?> Name,
    OptionalValue<string?> DisplayName,
    OptionalValue<bool> IsEnabled,
    OptionalValue<JsonElement?> Configuration,
    OptionalValue<JsonElement?> SecretConfiguration);

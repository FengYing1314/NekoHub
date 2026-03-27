using System.Text.Json;

namespace NekoHub.Application.Storage.Commands;

public sealed record CreateStorageProviderProfileCommand(
    string? Name,
    string? DisplayName,
    string? ProviderType,
    bool IsEnabled,
    bool IsDefault,
    JsonElement? Configuration,
    JsonElement? SecretConfiguration);

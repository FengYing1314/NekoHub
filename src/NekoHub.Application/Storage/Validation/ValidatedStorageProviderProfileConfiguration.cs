using NekoHub.Domain.Storage;

namespace NekoHub.Application.Storage.Validation;

public sealed record ValidatedStorageProviderProfileConfiguration(
    string ConfigurationJson,
    string? SecretConfigurationJson,
    StorageProviderCapabilities Capabilities);

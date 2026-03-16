namespace NekoHub.Application.Storage.Validation;

public interface IStorageProviderProfileConfigurationValidator
{
    string ProviderType { get; }

    ValidatedStorageProviderProfileConfiguration Validate(
        string configurationJson,
        string? secretConfigurationJson);
}

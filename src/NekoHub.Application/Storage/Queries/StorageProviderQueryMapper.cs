using System.Text.Json;
using NekoHub.Application.Storage.Queries.Dtos;
using NekoHub.Domain.Storage;

namespace NekoHub.Application.Storage.Queries;

internal static class StorageProviderQueryMapper
{
    public static StorageProviderProfileQueryDto ToProfileDto(StorageProviderProfile profile)
    {
        return new StorageProviderProfileQueryDto(
            Id: profile.Id,
            Name: profile.Name,
            DisplayName: profile.DisplayName,
            ProviderType: profile.ProviderType,
            IsEnabled: profile.IsEnabled,
            IsDefault: profile.IsDefault,
            Capabilities: ToCapabilitiesDto(profile.GetCapabilities()),
            ConfigurationSummary: BuildConfigurationSummary(profile.ConfigurationJson),
            CreatedAtUtc: profile.CreatedAtUtc,
            UpdatedAtUtc: profile.UpdatedAtUtc);
    }

    public static StorageProviderCapabilitiesQueryDto ToCapabilitiesDto(StorageProviderCapabilities capabilities)
    {
        return new StorageProviderCapabilitiesQueryDto(
            SupportsPublicRead: capabilities.SupportsPublicRead,
            SupportsPrivateRead: capabilities.SupportsPrivateRead,
            SupportsVisibilityToggle: capabilities.SupportsVisibilityToggle,
            SupportsDelete: capabilities.SupportsDelete,
            SupportsDirectPublicUrl: capabilities.SupportsDirectPublicUrl,
            RequiresAccessProxy: capabilities.RequiresAccessProxy,
            RecommendedForPrimaryStorage: capabilities.RecommendedForPrimaryStorage,
            IsPlatformBacked: capabilities.IsPlatformBacked,
            IsExperimental: capabilities.IsExperimental,
            RequiresTokenForPrivateRead: capabilities.RequiresTokenForPrivateRead);
    }

    public static StorageProviderConfigurationSummaryQueryDto BuildConfigurationSummary(string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return EmptyConfigurationSummary();
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return EmptyConfigurationSummary();
            }

            var root = document.RootElement;
            return new StorageProviderConfigurationSummaryQueryDto(
                ProviderName: TryGetString(root, "providerName"),
                RootPath: TryGetString(root, "rootPath"),
                EndpointHost: TryGetEndpointHost(root),
                BucketOrContainer: TryGetString(root, "bucket", "container"),
                Region: TryGetString(root, "region"),
                PublicBaseUrl: TryGetString(root, "publicBaseUrl"),
                ForcePathStyle: TryGetBoolean(root, "forcePathStyle"),
                Owner: TryGetString(root, "owner"),
                Repository: TryGetString(root, "repo", "repository"),
                Reference: TryGetString(root, "ref", "branch"),
                ReleaseTagMode: TryGetString(root, "releaseTagMode"),
                FixedTag: TryGetString(root, "fixedTag"),
                PathPrefix: TryGetString(root, "assetPathPrefix", "basePath"),
                VisibilityPolicy: TryGetString(root, "visibilityPolicy"),
                BasePath: TryGetString(root, "basePath"),
                AssetPathPrefix: TryGetString(root, "assetPathPrefix"),
                ApiBaseUrl: TryGetString(root, "apiBaseUrl"),
                RawBaseUrl: TryGetString(root, "rawBaseUrl"));
        }
        catch (JsonException)
        {
            return EmptyConfigurationSummary();
        }
    }

    private static StorageProviderConfigurationSummaryQueryDto EmptyConfigurationSummary()
    {
        return new StorageProviderConfigurationSummaryQueryDto(
            ProviderName: null,
            RootPath: null,
            EndpointHost: null,
            BucketOrContainer: null,
            Region: null,
            PublicBaseUrl: null,
            ForcePathStyle: null,
            Owner: null,
            Repository: null,
            Reference: null,
            ReleaseTagMode: null,
            FixedTag: null,
            PathPrefix: null,
            VisibilityPolicy: null,
            BasePath: null,
            AssetPathPrefix: null,
            ApiBaseUrl: null,
            RawBaseUrl: null);
    }

    private static string? TryGetEndpointHost(JsonElement root)
    {
        var endpoint = TryGetString(root, "endpoint");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.IsDefaultPort
            ? uri.Host
            : $"{uri.Host}:{uri.Port}";
    }

    private static string? TryGetString(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var property, names))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            _ => null
        };
    }

    private static bool? TryGetBoolean(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var property, names))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }

    private static bool TryGetProperty(JsonElement root, out JsonElement property, params string[] names)
    {
        foreach (var jsonProperty in root.EnumerateObject())
        {
            if (names.Any(name => string.Equals(jsonProperty.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                property = jsonProperty.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}

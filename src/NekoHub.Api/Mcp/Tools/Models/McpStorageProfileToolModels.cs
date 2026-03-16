using NekoHub.Application.Storage.Dtos;
using NekoHub.Application.Storage.Queries.Dtos;

namespace NekoHub.Api.Mcp.Tools.Models;

public sealed record McpStorageProviderCapabilitiesView(
    bool SupportsPublicRead,
    bool SupportsPrivateRead,
    bool SupportsVisibilityToggle,
    bool SupportsDelete,
    bool SupportsDirectPublicUrl,
    bool RequiresAccessProxy,
    bool RecommendedForPrimaryStorage,
    bool IsPlatformBacked,
    bool IsExperimental,
    bool RequiresTokenForPrivateRead);

public sealed record McpStorageProviderConfigurationSummaryView(
    string? ProviderName,
    string? RootPath,
    string? EndpointHost,
    string? BucketOrContainer,
    string? Region,
    string? PublicBaseUrl,
    bool? ForcePathStyle,
    string? Owner,
    string? Repository,
    string? Reference,
    string? ReleaseTagMode,
    string? FixedTag,
    string? PathPrefix,
    string? VisibilityPolicy,
    string? BasePath,
    string? AssetPathPrefix,
    string? ApiBaseUrl,
    string? RawBaseUrl);

public sealed record McpStorageProfileView(
    Guid Id,
    string Name,
    string? DisplayName,
    string ProviderType,
    bool IsEnabled,
    bool IsDefault,
    McpStorageProviderCapabilitiesView Capabilities,
    McpStorageProviderConfigurationSummaryView ConfigurationSummary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record McpStorageProfileListView(IReadOnlyList<McpStorageProfileView> Profiles);

public sealed record McpDeleteStorageProfileView(
    Guid Id,
    bool WasDefault,
    string Status,
    DateTimeOffset DeletedAtUtc);

public static class McpStorageProfileToolModelMapper
{
    public static McpStorageProfileListView ToView(IReadOnlyList<StorageProviderProfileQueryDto> profiles)
    {
        return new McpStorageProfileListView(profiles.Select(ToView).ToList());
    }

    public static McpStorageProfileView ToView(StorageProviderProfileQueryDto dto)
    {
        return new McpStorageProfileView(
            Id: dto.Id,
            Name: dto.Name,
            DisplayName: dto.DisplayName,
            ProviderType: dto.ProviderType,
            IsEnabled: dto.IsEnabled,
            IsDefault: dto.IsDefault,
            Capabilities: new McpStorageProviderCapabilitiesView(
                SupportsPublicRead: dto.Capabilities.SupportsPublicRead,
                SupportsPrivateRead: dto.Capabilities.SupportsPrivateRead,
                SupportsVisibilityToggle: dto.Capabilities.SupportsVisibilityToggle,
                SupportsDelete: dto.Capabilities.SupportsDelete,
                SupportsDirectPublicUrl: dto.Capabilities.SupportsDirectPublicUrl,
                RequiresAccessProxy: dto.Capabilities.RequiresAccessProxy,
                RecommendedForPrimaryStorage: dto.Capabilities.RecommendedForPrimaryStorage,
                IsPlatformBacked: dto.Capabilities.IsPlatformBacked,
                IsExperimental: dto.Capabilities.IsExperimental,
                RequiresTokenForPrivateRead: dto.Capabilities.RequiresTokenForPrivateRead),
            ConfigurationSummary: new McpStorageProviderConfigurationSummaryView(
                ProviderName: dto.ConfigurationSummary.ProviderName,
                RootPath: dto.ConfigurationSummary.RootPath,
                EndpointHost: dto.ConfigurationSummary.EndpointHost,
                BucketOrContainer: dto.ConfigurationSummary.BucketOrContainer,
                Region: dto.ConfigurationSummary.Region,
                PublicBaseUrl: dto.ConfigurationSummary.PublicBaseUrl,
                ForcePathStyle: dto.ConfigurationSummary.ForcePathStyle,
                Owner: dto.ConfigurationSummary.Owner,
                Repository: dto.ConfigurationSummary.Repository,
                Reference: dto.ConfigurationSummary.Reference,
                ReleaseTagMode: dto.ConfigurationSummary.ReleaseTagMode,
                FixedTag: dto.ConfigurationSummary.FixedTag,
                PathPrefix: dto.ConfigurationSummary.PathPrefix,
                VisibilityPolicy: dto.ConfigurationSummary.VisibilityPolicy,
                BasePath: dto.ConfigurationSummary.BasePath,
                AssetPathPrefix: dto.ConfigurationSummary.AssetPathPrefix,
                ApiBaseUrl: dto.ConfigurationSummary.ApiBaseUrl,
                RawBaseUrl: dto.ConfigurationSummary.RawBaseUrl),
            CreatedAtUtc: dto.CreatedAtUtc,
            UpdatedAtUtc: dto.UpdatedAtUtc);
    }

    public static McpDeleteStorageProfileView ToView(DeleteStorageProviderProfileResultDto dto)
    {
        return new McpDeleteStorageProfileView(
            Id: dto.Id,
            WasDefault: dto.WasDefault,
            Status: dto.Status,
            DeletedAtUtc: dto.DeletedAtUtc);
    }
}

using NekoHub.Application.Abstractions.Storage;

namespace NekoHub.Infrastructure.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; init; } = StorageProvider.Local.ToProviderName();

    public string? PublicBaseUrl { get; init; }
}

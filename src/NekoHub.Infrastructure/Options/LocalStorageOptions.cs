namespace NekoHub.Infrastructure.Options;

public sealed class LocalStorageOptions
{
    public const string SectionName = "Storage:Local";

    public string RootPath { get; init; } = "storage/assets";

    public bool CreateDirectoryIfMissing { get; init; } = true;
}

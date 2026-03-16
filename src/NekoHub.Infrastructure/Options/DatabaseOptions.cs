namespace NekoHub.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Persistence:Database";

    public string Provider { get; init; } = "sqlite";

    public string ConnectionString { get; init; } = "Data Source=storage/nekohub.db";
}

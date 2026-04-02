namespace NekoHub.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Persistence:Database";

    public string Provider { get; init; } = "postgresql";

    public string ConnectionString { get; init; } =
        "Host=localhost;Port=5432;Database=nekohub;Username=nekohub;Password=nekohub-dev;Pooling=true";
}

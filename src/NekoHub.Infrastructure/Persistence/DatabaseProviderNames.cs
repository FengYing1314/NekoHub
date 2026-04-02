namespace NekoHub.Infrastructure.Persistence;

internal static class DatabaseProviderNames
{
    public const string PostgreSql = "postgresql";

    public static string Normalize(string? provider)
    {
        if (string.Equals(provider, PostgreSql, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "postgres", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return PostgreSql;
        }

        throw new InvalidOperationException(
            $"Unsupported database provider '{provider}'. PostgreSQL is the only supported provider.");
    }

    public static bool IsPostgreSql(string? provider)
    {
        return string.Equals(Normalize(provider), PostgreSql, StringComparison.OrdinalIgnoreCase);
    }
}

namespace NekoHub.Infrastructure.Persistence;

internal static class DatabaseProviderNames
{
    public const string Sqlite = "sqlite";
    public const string PostgreSql = "postgresql";

    public static string Normalize(string? provider)
    {
        if (string.Equals(provider, Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            return Sqlite;
        }

        if (string.Equals(provider, PostgreSql, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "postgres", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return PostgreSql;
        }

        throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
    }

    public static bool IsSqlite(string? provider)
    {
        return string.Equals(provider, Sqlite, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPostgreSql(string? provider)
    {
        return string.Equals(Normalize(provider), PostgreSql, StringComparison.OrdinalIgnoreCase);
    }
}

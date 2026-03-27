using Microsoft.EntityFrameworkCore.Migrations;

namespace NekoHub.Infrastructure.Persistence.Migrations;

internal static class MigrationColumnTypes
{
    private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";
    private const string PostgreSqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    public static string Guid(MigrationBuilder migrationBuilder)
    {
        EnsureSupported(migrationBuilder);
        return IsSqlite(migrationBuilder) ? "TEXT" : "uuid";
    }

    public static string String(MigrationBuilder migrationBuilder, int? maxLength = null)
    {
        EnsureSupported(migrationBuilder);
        if (IsSqlite(migrationBuilder))
        {
            return "TEXT";
        }

        return maxLength.HasValue
            ? $"character varying({maxLength.Value})"
            : "text";
    }

    public static string Long(MigrationBuilder migrationBuilder)
    {
        EnsureSupported(migrationBuilder);
        return IsSqlite(migrationBuilder) ? "INTEGER" : "bigint";
    }

    public static string Int(MigrationBuilder migrationBuilder)
    {
        EnsureSupported(migrationBuilder);
        return IsSqlite(migrationBuilder) ? "INTEGER" : "integer";
    }

    public static string Boolean(MigrationBuilder migrationBuilder)
    {
        EnsureSupported(migrationBuilder);
        return IsSqlite(migrationBuilder) ? "INTEGER" : "boolean";
    }

    private static bool IsSqlite(MigrationBuilder migrationBuilder)
    {
        return string.Equals(migrationBuilder.ActiveProvider, SqliteProvider, StringComparison.Ordinal);
    }

    private static bool IsPostgreSql(MigrationBuilder migrationBuilder)
    {
        return string.Equals(migrationBuilder.ActiveProvider, PostgreSqlProvider, StringComparison.Ordinal);
    }

    private static void EnsureSupported(MigrationBuilder migrationBuilder)
    {
        if (IsSqlite(migrationBuilder) || IsPostgreSql(migrationBuilder))
        {
            return;
        }

        throw new InvalidOperationException($"Unsupported migration provider '{migrationBuilder.ActiveProvider}'.");
    }
}

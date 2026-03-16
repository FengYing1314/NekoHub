using Microsoft.Data.Sqlite;

namespace NekoHub.Infrastructure.Persistence;

internal static class SqliteConnectionStringResolver
{
    public static string Resolve(string connectionString, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (!TryGetFilePathDataSource(builder.DataSource, out var dataSource))
        {
            return builder.ToString();
        }

        var absolutePath = Path.IsPathRooted(dataSource)
            ? Path.GetFullPath(dataSource)
            : Path.GetFullPath(Path.Combine(contentRootPath, dataSource));

        builder.DataSource = absolutePath;
        return builder.ToString();
    }

    public static string? TryGetDatabaseDirectory(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (!TryGetFilePathDataSource(builder.DataSource, out var dataSource))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(dataSource);
        return Path.GetDirectoryName(fullPath);
    }

    private static bool TryGetFilePathDataSource(string? dataSource, out string value)
    {
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return false;
        }

        if (string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = dataSource;
        return true;
    }
}

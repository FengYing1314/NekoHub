using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;

namespace NekoHub.Api.IntegrationTests.Setup;

internal static class PostgresTestEnvironment
{
    private const int PostgreSqlPort = 5432;
    private const string DefaultImage = "postgres:17-alpine";
    private const string DefaultAdminDatabase = "postgres";
    private const string DefaultUsername = "nekohub_test";
    private const string DefaultPassword = "nekohub_test";

    // 同一进程内只初始化一次 PostgreSQL 服务端，数据库级隔离通过每次租约创建独立库完成。
    private static readonly SemaphoreSlim SyncRoot = new(1, 1);

    private static IContainer? _container;
    private static string? _adminConnectionString;

    public static PostgresTestDatabaseLease CreateDatabaseLease(string prefix)
    {
        return CreateDatabaseLeaseAsync(prefix).GetAwaiter().GetResult();
    }

    public static async Task<PostgresTestDatabaseLease> CreateDatabaseLeaseAsync(string prefix)
    {
        var databaseName = BuildDatabaseName(prefix);
        await EnsureServerReadyAsync();
        var connectionString = await CreateDatabaseAsync(databaseName);
        return new PostgresTestDatabaseLease(databaseName, connectionString);
    }

    public static void DropDatabase(string databaseName)
    {
        DropDatabaseAsync(databaseName).GetAwaiter().GetResult();
    }

    private static async Task<string> CreateDatabaseAsync(string databaseName)
    {
        var adminConnectionString = await GetAdminConnectionStringAsync();

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        // 同名前缀的数据库先强制删除再创建，保证重复运行同一测试时仍是干净状态。
        await using (var dropCommand = connection.CreateCommand())
        {
            dropCommand.CommandText = $"""DROP DATABASE IF EXISTS "{databaseName}" WITH (FORCE);""";
            await dropCommand.ExecuteNonQueryAsync();
        }

        await using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = $"""CREATE DATABASE "{databaseName}";""";
            await createCommand.ExecuteNonQueryAsync();
        }

        return BuildDatabaseConnectionString(adminConnectionString, databaseName);
    }

    private static async Task DropDatabaseAsync(string databaseName)
    {
        await EnsureServerReadyAsync();

        var adminConnectionString = await GetAdminConnectionStringAsync();
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"""DROP DATABASE IF EXISTS "{databaseName}" WITH (FORCE);""";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureServerReadyAsync()
    {
        if (!string.IsNullOrWhiteSpace(_adminConnectionString))
        {
            return;
        }

        await SyncRoot.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(_adminConnectionString))
            {
                return;
            }

            var configuredConnectionString = Environment.GetEnvironmentVariable("NEKOHUB_TEST_DATABASE_CONNECTIONSTRING");
            if (!string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                // 显式提供连接串时复用外部 PostgreSQL，不再启动 Testcontainers 容器。
                _adminConnectionString = BuildDatabaseConnectionString(configuredConnectionString, DefaultAdminDatabase);
                return;
            }

            _container = new ContainerBuilder(DefaultImage)
                .WithName($"nekohub-postgres-tests-{Guid.NewGuid():N}")
                .WithPortBinding(PostgreSqlPort, true)
                .WithEnvironment("POSTGRES_DB", DefaultAdminDatabase)
                .WithEnvironment("POSTGRES_USER", DefaultUsername)
                .WithEnvironment("POSTGRES_PASSWORD", DefaultPassword)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(PostgreSqlPort))
                .WithCleanUp(true)
                .Build();

            await _container.StartAsync();

            _adminConnectionString = new NpgsqlConnectionStringBuilder
            {
                Host = _container.Hostname,
                Port = _container.GetMappedPublicPort(PostgreSqlPort),
                Database = DefaultAdminDatabase,
                Username = DefaultUsername,
                Password = DefaultPassword,
                Pooling = false,
                IncludeErrorDetail = true
            }.ConnectionString;

            // 端口开放不代表数据库已经接受 SQL 连接，这里再做一轮主动探测。
            await WaitUntilAcceptingConnectionsAsync(_adminConnectionString);
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    private static async Task<string> GetAdminConnectionStringAsync()
    {
        await EnsureServerReadyAsync();
        return _adminConnectionString
               ?? throw new InvalidOperationException("PostgreSQL test environment is not initialized.");
    }

    private static string BuildDatabaseConnectionString(string baseConnectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = databaseName,
            Pooling = false,
            IncludeErrorDetail = true
        };

        return builder.ConnectionString;
    }

    private static async Task WaitUntilAcceptingConnectionsAsync(string adminConnectionString)
    {
        const int maxAttempts = 30;

        for (var attempt = 1; attempt <= maxAttempts; attempt += 1)
        {
            try
            {
                await using var connection = new NpgsqlConnection(adminConnectionString);
                await connection.OpenAsync();
                return;
            }
            catch when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new InvalidOperationException("PostgreSQL test environment did not become ready in time.");
    }

    private static string BuildDatabaseName(string prefix)
    {
        var normalizedPrefix = new string(prefix
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray());

        if (string.IsNullOrWhiteSpace(normalizedPrefix))
        {
            normalizedPrefix = "nekohub_it";
        }

        return $"{normalizedPrefix}_{Guid.NewGuid():N}";
    }
}

internal sealed class PostgresTestDatabaseLease(string databaseName, string connectionString) : IAsyncDisposable
{
    public string DatabaseName { get; } = databaseName;

    public string ConnectionString { get; } = connectionString;

    public ValueTask DisposeAsync()
    {
        // lease 释放即销毁整库，确保测试之间不会残留 schema 或数据。
        PostgresTestEnvironment.DropDatabase(DatabaseName);
        return ValueTask.CompletedTask;
    }
}

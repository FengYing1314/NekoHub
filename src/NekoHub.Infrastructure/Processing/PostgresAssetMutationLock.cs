using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Infrastructure.Persistence;
using Npgsql;

namespace NekoHub.Infrastructure.Processing;

public sealed class PostgresAssetMutationLock(AssetDbContext dbContext) : IAssetMutationLock
{
    public async Task<IAsyncDisposable> AcquireAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var key = BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(assetId.ToByteArray()));
        var connection = new NpgsqlConnection(dbContext.Database.GetConnectionString());
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", connection) { CommandTimeout = 0 };
            command.Parameters.AddWithValue("key", key);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new Lease(connection, key);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Lease(NpgsqlConnection connection, long key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
                command.Parameters.AddWithValue("key", key);
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}

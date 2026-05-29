using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WSM.Core.Interfaces;
using WSM.Core.Models;
using WSM.Infrastructure.Paths;

namespace WSM.Infrastructure.Persistence;

/// <summary>
/// SQLite 托管服务仓库。
/// </summary>
public sealed class SqliteServiceRepository : IServiceRepository, IDisposable
{
    private readonly WsmPaths _paths;
    private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
    private string? _initializedDatabasePath;

    public SqliteServiceRepository(WsmPaths paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<ManagedService>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<ManagedService>();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT json FROM managed_services ORDER BY display_name;";
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        results.Add(ManagedServiceSerializer.Deserialize(reader.GetString(0)));
                    }
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        return results;
    }

    public async Task<ManagedService?> GetByIdAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT json FROM managed_services WHERE id = $id LIMIT 1;";
                command.Parameters.AddWithValue("$id", serviceId);

                var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (scalar == null || scalar == DBNull.Value)
                {
                    return null;
                }

                var jsonText = Convert.ToString(scalar);
                if (string.IsNullOrWhiteSpace(jsonText))
                {
                    return null;
                }

                return ManagedServiceSerializer.Deserialize(jsonText);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(ManagedService service, CancellationToken cancellationToken = default)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        service.UpdatedAt = DateTime.UtcNow;
        if (service.CreatedAt == default)
        {
            service.CreatedAt = service.UpdatedAt;
        }

        var json = ManagedServiceSerializer.Serialize(service);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO managed_services (id, display_name, json, created_at, updated_at)
VALUES ($id, $displayName, $json, $createdAt, $updatedAt)
ON CONFLICT(id) DO UPDATE SET
  display_name = excluded.display_name,
  json = excluded.json,
  updated_at = excluded.updated_at;";

                command.Parameters.AddWithValue("$id", service.Id);
                command.Parameters.AddWithValue("$displayName", service.DisplayName);
                command.Parameters.AddWithValue("$json", json);
                command.Parameters.AddWithValue("$createdAt", service.CreatedAt.ToString("O"));
                command.Parameters.AddWithValue("$updatedAt", service.UpdatedAt.ToString("O"));

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM managed_services WHERE id = $id;";
                command.Parameters.AddWithValue("$id", serviceId);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ExistsAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var service = await GetByIdAsync(serviceId, cancellationToken).ConfigureAwait(false);
        return service != null;
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(_initializedDatabasePath, _paths.DatabasePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (string.Equals(_initializedDatabasePath, _paths.DatabasePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _paths.EnsureLayout();

            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE TABLE IF NOT EXISTS managed_services (
  id TEXT PRIMARY KEY NOT NULL,
  display_name TEXT NOT NULL,
  json TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);";
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            _initializedDatabasePath = _paths.DatabasePath;
        }
        finally
        {
            _gate.Release();
        }
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_paths.DatabasePath}");
        connection.Open();
        return connection;
    }
}

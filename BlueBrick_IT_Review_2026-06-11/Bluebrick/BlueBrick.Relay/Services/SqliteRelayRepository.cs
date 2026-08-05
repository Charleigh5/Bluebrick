using BlueBrick.Relay.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace BlueBrick.Relay.Services;

public sealed class SqliteRelayRepository : IRelayRepository
{
    private readonly string _connectionString;

    public SqliteRelayRepository(IOptions<RelayOptions> options)
    {
        var dbPath = Path.GetFullPath(options.Value.SqlitePath);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS devices (
  device_id TEXT PRIMARY KEY,
  device_name TEXT NOT NULL,
  product TEXT NOT NULL,
  updated_utc TEXT NOT NULL,
  last_heartbeat_utc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS session_routes (
  session_id TEXT PRIMARY KEY,
  device_id TEXT NOT NULL,
  updated_utc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS audit_log (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  category TEXT NOT NULL,
  action TEXT NOT NULL,
  session_id TEXT NULL,
  status TEXT NOT NULL,
  detail TEXT NOT NULL,
  created_utc TEXT NOT NULL
);";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertDeviceAsync(RelayDeviceRegistration device, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO devices(device_id, device_name, product, updated_utc, last_heartbeat_utc)
VALUES($deviceId, $deviceName, $product, $updatedUtc, $updatedUtc)
ON CONFLICT(device_id) DO UPDATE SET
  device_name = excluded.device_name,
  product = excluded.product,
  updated_utc = excluded.updated_utc;";
        var now = DateTime.UtcNow.ToString("O");
        command.Parameters.AddWithValue("$deviceId", device.DeviceId);
        command.Parameters.AddWithValue("$deviceName", device.DeviceName);
        command.Parameters.AddWithValue("$product", device.Product);
        command.Parameters.AddWithValue("$updatedUtc", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task TouchHeartbeatAsync(string deviceId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = @"UPDATE devices SET last_heartbeat_utc = $updatedUtc WHERE device_id = $deviceId;";
        command.Parameters.AddWithValue("$deviceId", deviceId);
        command.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertSessionRouteAsync(RelaySessionRoute route, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO session_routes(session_id, device_id, updated_utc)
VALUES($sessionId, $deviceId, $updatedUtc)
ON CONFLICT(session_id) DO UPDATE SET
  device_id = excluded.device_id,
  updated_utc = excluded.updated_utc;";
        command.Parameters.AddWithValue("$sessionId", route.SessionId);
        command.Parameters.AddWithValue("$deviceId", route.DeviceId);
        command.Parameters.AddWithValue("$updatedUtc", route.UpdatedUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<RelaySessionRoute?> GetSessionRouteAsync(string sessionId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = @"SELECT session_id, device_id, updated_utc FROM session_routes WHERE session_id = $sessionId;";
        command.Parameters.AddWithValue("$sessionId", sessionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RelaySessionRoute
        {
            SessionId = reader.GetString(0),
            DeviceId = reader.GetString(1),
            UpdatedUtc = DateTime.Parse(reader.GetString(2))
        };
    }

    public async Task WriteAuditAsync(string category, string action, string? sessionId, string status, string detail, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO audit_log(category, action, session_id, status, detail, created_utc)
VALUES($category, $action, $sessionId, $status, $detail, $createdUtc);";
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$sessionId", (object?)sessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$detail", detail);
        command.Parameters.AddWithValue("$createdUtc", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

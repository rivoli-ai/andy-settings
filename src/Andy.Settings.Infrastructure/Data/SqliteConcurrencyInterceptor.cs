using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Andy.Settings.Infrastructure.Data;

/// <summary>
/// Applies the SQLite pragmas this service needs for concurrent access, on
/// every connection as it opens.
/// </summary>
/// <remarks>
/// In the Conductor-embedded deployment a single SQLite file is written
/// concurrently by request threads, the OutboxDispatcher (polling every
/// second), the SeenMessages cleanup job, and a separate SaveChanges per audit
/// row. Contention is routine there, not exceptional
/// (rivoli-ai/andy-settings#145).
///
/// Two things were missing, both verified against a real connection:
///
/// <list type="bullet">
/// <item><c>PRAGMA busy_timeout</c> was <b>0</b> — a writer finding the
/// database locked failed immediately with SQLITE_BUSY. Note that
/// <c>SqliteConnectionStringBuilder.DefaultTimeout</c> already defaults to 30,
/// but that is a command-level retry in Microsoft.Data.Sqlite, not the SQLite
/// busy handler; setting it changes nothing here.</item>
/// <item><c>journal_mode</c> was <c>delete</c>, which takes an exclusive lock
/// for the duration of every write. WAL lets readers proceed during a write,
/// which is the actual fix for a reader/writer mix like this one.</item>
/// </list>
/// </remarks>
public sealed class SqliteConcurrencyInterceptor : DbConnectionInterceptor
{
    /// <summary>
    /// Long enough to ride out the outbox dispatcher's write burst, short
    /// enough that a genuinely wedged database still surfaces as an error.
    /// </summary>
    public const int BusyTimeoutMilliseconds = 30_000;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Apply(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Apply(connection);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void Apply(DbConnection connection)
    {
        if (connection is not SqliteConnection sqlite)
            return;

        using var command = sqlite.CreateCommand();

        command.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds};";
        command.ExecuteNonQuery();

        // WAL is meaningless for an in-memory database and SQLite silently
        // keeps `memory` journal mode there, so this is skipped rather than
        // relying on that. Guarding also keeps the integration-test harness
        // (shared-cache in-memory) on its expected mode.
        if (IsFileBacked(sqlite))
        {
            command.CommandText = "PRAGMA journal_mode = WAL;";
            command.ExecuteNonQuery();
        }
    }

    // Inspects the CONNECTION STRING, not SqliteConnection.DataSource:
    // for the URI form (`Data Source=file:name?Mode=Memory&Cache=Shared`)
    // DataSource reports only the bare name, dropping the Mode parameter, so a
    // shared-cache in-memory database reads as file-backed.
    private static bool IsFileBacked(SqliteConnection connection)
    {
        var connectionString = connection.ConnectionString;
        if (string.IsNullOrEmpty(connectionString))
            return false;

        return !connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase)
               && !connectionString.Contains("mode=memory", StringComparison.OrdinalIgnoreCase);
    }
}

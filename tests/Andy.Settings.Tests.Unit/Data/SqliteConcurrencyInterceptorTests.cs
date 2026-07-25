using Andy.Settings.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Tests.Unit.Data;

// rivoli-ai/andy-settings#145. Verified against a real connection before the
// fix: PRAGMA busy_timeout was 0 (a writer finding the database locked failed
// immediately) and journal_mode was `delete` (every write takes an exclusive
// lock). Both matter in the Conductor-embedded deployment, where one file is
// written concurrently by request threads, the outbox dispatcher, the cleanup
// job, and a SaveChanges per audit row.
public class SqliteConcurrencyInterceptorTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"andy-settings-pragma-{Guid.NewGuid():N}.sqlite");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private SettingsDbContext FileBackedContext() => new(
        new DbContextOptionsBuilder<SettingsDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .AddInterceptors(new SqliteConcurrencyInterceptor())
            .Options);

    private static T Pragma<T>(SettingsDbContext db, string pragma)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragma};";
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T));
    }

    [Fact]
    public void FileBackedConnection_SetsBusyTimeout()
    {
        using var db = FileBackedContext();
        db.Database.EnsureCreated();

        Pragma<long>(db, "busy_timeout")
            .Should().Be(SqliteConcurrencyInterceptor.BusyTimeoutMilliseconds);
    }

    [Fact]
    public void FileBackedConnection_EnablesWal()
    {
        using var db = FileBackedContext();
        db.Database.EnsureCreated();

        Pragma<string>(db, "journal_mode").Should().Be("wal");
    }

    // WAL is meaningless in memory, and the integration-test harness runs on a
    // shared-cache in-memory database — it must keep its own journal mode.
    // WAL must not be attempted against an in-memory database. Asserted on the
    // journal mode itself rather than by re-reading a pragma through a
    // test-opened connection: SqliteConnection pools, so re-opening can hand
    // back a connection whose pragmas EF already set, which makes such an
    // assertion measure the pool rather than this interceptor.
    //
    // The in-memory harness staying healthy is covered end to end by the 39
    // integration tests, which run on a shared-cache in-memory database.
    //
    // Note `Data Source=file:name?Mode=Memory` (capital M inside a URI) is NOT
    // in-memory: SQLite parses URI parameters case-sensitively, ignores it, and
    // opens a real file — so treating that form as file-backed is correct.
    [Theory]
    [InlineData("Data Source=pragma-probe;Mode=Memory;Cache=Shared")]
    [InlineData("Data Source=file:pragma-probe-uri?mode=memory&cache=shared")]
    [InlineData("Data Source=:memory:")]
    public void InMemoryConnectionString_DoesNotEnableWal(string connectionString)
    {
        using var db = new SettingsDbContext(
            new DbContextOptionsBuilder<SettingsDbContext>()
                .UseSqlite(connectionString)
                .AddInterceptors(new SqliteConcurrencyInterceptor())
                .Options);
        db.Database.EnsureCreated();

        Pragma<string>(db, "journal_mode").Should().Be("memory", "WAL does not apply in memory");
    }

    // Known limitation, pinned so it is a documented property rather than a
    // surprise: EF only raises ConnectionOpened for connections IT opens. When
    // a caller hands EF an already-open DbConnection — as the integration-test
    // harness does — the interceptor never fires and the pragmas are not
    // applied. That is harmless for the in-memory test harness, but anything
    // production-like must pass a connection STRING, not an open connection.
    [Fact]
    public void ExternallyOpenedConnection_DoesNotReceivePragmas()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var db = new SettingsDbContext(
            new DbContextOptionsBuilder<SettingsDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(new SqliteConcurrencyInterceptor())
                .Options);
        db.Database.EnsureCreated();

        Pragma<long>(db, "busy_timeout").Should().Be(0);
    }
}

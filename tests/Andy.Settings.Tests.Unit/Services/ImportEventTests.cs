using System.Text;
using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.ImportExport;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Andy.Settings.Tests.Unit.Services;

// rivoli-ai/andy-settings#135. Import wrote assignments straight to the
// database and published nothing, so every consumer kept serving the
// pre-import value indefinitely — they invalidate on events. It also recorded
// no audit rows despite taking an actorId and despite AuditEventType.Imported
// existing for exactly this purpose.
//
// Real SQLite: ImportAsync opens a transaction, which the InMemory provider
// does not support.
public class ImportEventTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SettingsDbContext _db;
    private readonly Mock<IAuditService> _audit = new();
    private readonly ExportImportService _sut;

    public ImportEventTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new SettingsDbContext(
            new DbContextOptionsBuilder<SettingsDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _audit.Setup(a => a.RecordAsync(It.IsAny<AuditEventDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new ExportImportService(_db, new ValidationService(), _audit.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static Stream Document(string valueJson) => new MemoryStream(Encoding.UTF8.GetBytes($$"""
    {
      "definitions": [
        {
          "key": "app.imported.key",
          "applicationCode": "testapp",
          "displayName": "Imported",
          "dataType": "String"
        }
      ],
      "assignments": [
        {
          "definitionKey": "app.imported.key",
          "scopeType": "Machine",
          "scopeId": null,
          "valueJson": {{valueJson}}
        }
      ]
    }
    """));

    [Fact]
    public async Task ImportAsync_NewAssignment_EmitsCreatedEvent()
    {
        var result = await _sut.ImportAsync(Document("\"\\\"first\\\"\""), new ImportOptions(), "importer");

        result.AssignmentsCreated.Should().Be(1);

        var events = await _db.Outbox.ToListAsync();
        events.Should().ContainSingle().Which.Subject.Should().EndWith(".created");
    }

    [Fact]
    public async Task ImportAsync_ChangedAssignment_EmitsUpdatedEvent()
    {
        await _sut.ImportAsync(Document("\"\\\"first\\\"\""), new ImportOptions(), "importer");
        await _db.Outbox.ExecuteDeleteAsync();

        var result = await _sut.ImportAsync(Document("\"\\\"second\\\"\""), new ImportOptions(), "importer");

        result.AssignmentsUpdated.Should().Be(1);
        var events = await _db.Outbox.ToListAsync();
        events.Should().ContainSingle().Which.Subject.Should().EndWith(".updated");
    }

    // Re-importing the same document changes nothing, so it must not emit a
    // phantom event — consumers would invalidate for no reason.
    [Fact]
    public async Task ImportAsync_UnchangedAssignment_EmitsNothing()
    {
        await _sut.ImportAsync(Document("\"\\\"same\\\"\""), new ImportOptions(), "importer");
        await _db.Outbox.ExecuteDeleteAsync();

        var result = await _sut.ImportAsync(Document("\"\\\"same\\\"\""), new ImportOptions(), "importer");

        result.AssignmentsCreated.Should().Be(0);
        result.AssignmentsUpdated.Should().Be(0);
        (await _db.Outbox.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_RecordsImportedAuditWithBeforeAndAfter()
    {
        await _sut.ImportAsync(Document("\"\\\"first\\\"\""), new ImportOptions(), "importer");
        _audit.Invocations.Clear();

        await _sut.ImportAsync(Document("\"\\\"second\\\"\""), new ImportOptions(), "importer");

        _audit.Verify(a => a.RecordAsync(
            It.Is<AuditEventDto>(e =>
                e.EventType == AuditEventType.Imported &&
                e.DefinitionKey == "app.imported.key" &&
                e.ActorId == "importer" &&
                e.BeforeJson == "\"first\"" &&
                e.AfterJson == "\"second\""),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_DryRun_WritesNothing()
    {
        await _sut.ImportAsync(Document("\"\\\"v\\\"\""), new ImportOptions { DryRun = true }, "importer");

        (await _db.SettingAssignments.CountAsync()).Should().Be(0);
        (await _db.Outbox.CountAsync()).Should().Be(0);
        _audit.Verify(a => a.RecordAsync(It.IsAny<AuditEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Secrets;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Application.Messaging;
using Andy.Settings.Application.Messaging.Events;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Messaging;
using Andy.Settings.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Andy.Settings.Tests.Unit.Messaging;

// rivoli-ai/conductor#925 (M1.2.1). Contract + integration coverage
// for the outbox path SecretService writes to on Set / Rotate / Delete.
//
// Subject + payload contract (pinned here):
//   subject: `andy.settings.events.definition.<definitionKey>.<kind>`
//     - kind = "updated" for both Set and Rotated (collapsed so the
//       downstream consumer's existing wildcard works)
//     - kind = "deleted" for Delete
//   payload: SecretChangedPayload with mutation tag in {"set", "rotated", "revoked"}
//   payload NEVER contains the value (negative assertion below)
//
// Drift on any of these silently breaks andy-models' live-rotation
// cache invalidation — without the test, the user would notice as
// "I rotated the key and the container still sees the old one".
public class SecretEventOutboxTests : IDisposable
{
    private readonly SettingsDbContext _db;
    private readonly SecretService _sut;

    public SecretEventOutboxTests()
    {
        var options = new DbContextOptionsBuilder<SettingsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new SettingsDbContext(options);

        var dataProtectionProvider = DataProtectionProvider.Create("Tests");
        var auditMock = new Mock<IAuditService>();
        auditMock.Setup(a => a.RecordAsync(It.IsAny<AuditEventDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new SecretService(_db, dataProtectionProvider, auditMock.Object);
    }

    public void Dispose() => _db.Dispose();

    private async Task<SettingDefinition> SeedDefinition(
        string key = "andy.models.providers.anthropic.apiKey")
    {
        var def = new SettingDefinition
        {
            Id = Guid.NewGuid(),
            Key = key,
            ApplicationCode = "andy-models",
            DisplayName = "Anthropic API key",
            DataType = SettingDataType.Secret,
            IsSecret = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.SettingDefinitions.Add(def);
        await _db.SaveChangesAsync();
        return def;
    }

    // ---------- Subject taxonomy ----------

    [Theory]
    [InlineData(SecretEventKind.Set, "updated")]
    [InlineData(SecretEventKind.Rotated, "updated")]
    [InlineData(SecretEventKind.Deleted, "deleted")]
    public void SubjectFor_ProducesAndyModelsConsumerCompatibleShape(SecretEventKind kind, string expectedSuffix)
    {
        var subject = SecretEventOutbox.SubjectFor("andy.models.providers.anthropic.apiKey", kind);
        subject.Should().Be($"andy.settings.events.definition.andy.models.providers.anthropic.apiKey.{expectedSuffix}");
        // The downstream consumer subscribes to
        // `andy.settings.events.definition.>` and filters its handler
        // on `.updated` / `.deleted`. Drift in this prefix silently
        // disables live-rotation cache invalidation.
        subject.Should().StartWith(SecretEventOutbox.DefinitionEventSubjectPrefix);
    }

    [Fact]
    public void SubjectFor_RejectsEmptyDefinitionKey()
    {
        // An empty key would emit `andy.settings.events.definition..updated`
        // which the consumer's regex doesn't match — fail loudly here
        // instead of silently dropping events.
        var act = () => SecretEventOutbox.SubjectFor("", SecretEventKind.Set);
        act.Should().Throw<ArgumentException>();
    }

    // ---------- Outbox row content ----------

    [Fact]
    public async Task SetSecret_FirstTime_WritesSetMutationToOutbox()
    {
        await SeedDefinition();

        await _sut.SetSecretAsync(new SetSecretDto
        {
            DefinitionKey = "andy.models.providers.anthropic.apiKey",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "sk-ant-supersecret"
        }, actorId: "user-1");

        var entries = await _db.Outbox.ToListAsync();
        entries.Should().HaveCount(1, "first-time set must produce exactly one event");

        var payload = JsonSerializer.Deserialize<SecretChangedPayload>(
            entries[0].PayloadJson, EventJson.Options);
        payload.Should().NotBeNull();
        payload!.Mutation.Should().Be("set",
            "the first-time write must carry the `set` mutation tag — `rotated` would mislead the audit trail");
        payload.DefinitionKey.Should().Be("andy.models.providers.anthropic.apiKey");
        entries[0].Subject.Should().Be(
            "andy.settings.events.definition.andy.models.providers.anthropic.apiKey.updated");
    }

    [Fact]
    public async Task SetSecret_Overwrite_WritesRotatedMutation()
    {
        await SeedDefinition();
        await _sut.SetSecretAsync(new SetSecretDto
        {
            DefinitionKey = "andy.models.providers.anthropic.apiKey",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "sk-ant-v1"
        }, actorId: "user-1");
        await _sut.SetSecretAsync(new SetSecretDto
        {
            DefinitionKey = "andy.models.providers.anthropic.apiKey",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "sk-ant-v2"
        }, actorId: "user-1");

        var entries = await _db.Outbox.OrderBy(o => o.CreatedAt).ToListAsync();
        entries.Should().HaveCount(2);

        var second = JsonSerializer.Deserialize<SecretChangedPayload>(
            entries[1].PayloadJson, EventJson.Options);
        second!.Mutation.Should().Be("rotated",
            "overwrites must carry the `rotated` mutation tag so audit consumers can distinguish first-time-set from key-rotation events");
    }

    [Fact]
    public async Task RotateSecret_WritesRotatedMutation()
    {
        await SeedDefinition();
        // Seed an initial value so rotate has something to update.
        await _sut.SetSecretAsync(new SetSecretDto
        {
            DefinitionKey = "andy.models.providers.anthropic.apiKey",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "sk-ant-old"
        }, actorId: "user-1");

        await _sut.RotateSecretAsync(new RotateSecretDto
        {
            DefinitionKey = "andy.models.providers.anthropic.apiKey",
            ScopeType = ScopeType.Machine,
            NewPlaintextValue = "sk-ant-new"
        }, actorId: "user-2");

        var entries = await _db.Outbox.OrderBy(o => o.CreatedAt).ToListAsync();
        entries.Should().HaveCount(2);
        var rotateEvent = JsonSerializer.Deserialize<SecretChangedPayload>(
            entries[1].PayloadJson, EventJson.Options);
        rotateEvent!.Mutation.Should().Be("rotated");
    }

    [Fact]
    public async Task DeleteSecret_WritesRevokedMutationOnDeletedSubject()
    {
        await SeedDefinition();
        await _sut.SetSecretAsync(new SetSecretDto
        {
            DefinitionKey = "andy.models.providers.anthropic.apiKey",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "sk-ant-x"
        }, actorId: "user-1");

        await _sut.DeleteSecretAsync("andy.models.providers.anthropic.apiKey");

        var entries = await _db.Outbox.OrderBy(o => o.CreatedAt).ToListAsync();
        entries.Should().HaveCount(2);
        var deleteEvent = entries[1];
        deleteEvent.Subject.Should().EndWith(".deleted",
            "Delete is a structurally different event from update — the consumer routes the two differently");
        var payload = JsonSerializer.Deserialize<SecretChangedPayload>(
            deleteEvent.PayloadJson, EventJson.Options);
        payload!.Mutation.Should().Be("revoked",
            "the spec calls a delete `revoked` in the payload — audit consumers key off this tag");
    }

    [Fact]
    public async Task DeleteSecret_NoRows_DoesNotPublishPhantomEvent()
    {
        await SeedDefinition();
        // No SetSecret call — definition exists but no encrypted rows.

        await _sut.DeleteSecretAsync("andy.models.providers.anthropic.apiKey");

        var entries = await _db.Outbox.ToListAsync();
        entries.Should().BeEmpty(
            "a delete that found nothing must not announce a phantom revoke — consumers would react to a state that never existed");
    }

    // ---------- Negative assertion: the value is NEVER on the wire ----------

    [Fact]
    public async Task Payload_NeverContainsTheSecretValue()
    {
        await SeedDefinition();

        const string secret = "sk-ant-NEVER_ON_THE_WIRE_xyzzy";
        await _sut.SetSecretAsync(new SetSecretDto
        {
            DefinitionKey = "andy.models.providers.anthropic.apiKey",
            ScopeType = ScopeType.Machine,
            PlaintextValue = secret
        }, actorId: "user-1");

        var entries = await _db.Outbox.ToListAsync();
        entries.Should().HaveCount(1);

        var raw = entries[0].PayloadJson;
        raw.Should().NotContain(secret,
            "the secret VALUE must never appear in the payload — drift here is a security incident.");
        raw.Should().NotContain("EncryptedValue",
            "even the ciphertext field name must not bleed through; consumers re-resolve via REST.");
    }

    // ---------- Atomicity: rollback must not leave an outbox row ----------

    [Fact]
    public async Task SetSecret_OnUnknownDefinition_WritesNoOutboxRow()
    {
        // No SeedDefinition() — the call throws KeyNotFoundException
        // before SaveChangesAsync. The OutboxEntry we'd otherwise
        // append must not be there either (the in-memory provider
        // doesn't enforce atomicity the way Postgres does, but the
        // service must short-circuit BEFORE appending).
        var act = () => _sut.SetSecretAsync(new SetSecretDto
        {
            DefinitionKey = "missing.definition",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "x"
        }, actorId: "user-1");

        await act.Should().ThrowAsync<KeyNotFoundException>();

        (await _db.Outbox.AnyAsync()).Should().BeFalse(
            "a throw before SaveChangesAsync must not leave a half-written outbox row queued for publishing");
    }
}

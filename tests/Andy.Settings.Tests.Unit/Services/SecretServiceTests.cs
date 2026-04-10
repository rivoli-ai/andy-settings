using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Secrets;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Andy.Settings.Tests.Unit.Services;

public class SecretServiceTests : IDisposable
{
    private readonly SettingsDbContext _db;
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly SecretService _sut;

    public SecretServiceTests()
    {
        var options = new DbContextOptionsBuilder<SettingsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new SettingsDbContext(options);

        var dataProtectionProvider = DataProtectionProvider.Create("Tests");

        _auditMock.Setup(a => a.RecordAsync(It.IsAny<AuditEventDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new SecretService(_db, dataProtectionProvider, _auditMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<SettingDefinition> SeedDefinition(
        string key = "app.secret.key",
        bool isSecret = true)
    {
        var definition = new SettingDefinition
        {
            Id = Guid.NewGuid(),
            Key = key,
            ApplicationCode = "testapp",
            DisplayName = "Test Secret",
            DataType = isSecret ? SettingDataType.Secret : SettingDataType.String,
            IsSecret = isSecret,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.SettingDefinitions.Add(definition);
        await _db.SaveChangesAsync();
        return definition;
    }

    [Fact]
    public async Task SetSecret_EncryptsValue()
    {
        await SeedDefinition();

        var dto = new SetSecretDto
        {
            DefinitionKey = "app.secret.key",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "super-secret-password"
        };

        await _sut.SetSecretAsync(dto, "test-user");

        var stored = await _db.EncryptedSecrets.FirstAsync();
        stored.EncryptedValue.Should().NotBe("super-secret-password",
            "the stored value should be encrypted and differ from the plaintext");
        stored.EncryptedValue.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSecret_DecryptsValue_RoundTrip()
    {
        await SeedDefinition();

        var setDto = new SetSecretDto
        {
            DefinitionKey = "app.secret.key",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "super-secret-password"
        };
        await _sut.SetSecretAsync(setDto, "test-user");

        var getDto = new GetSecretDto
        {
            DefinitionKey = "app.secret.key",
            ScopeType = ScopeType.Machine
        };
        var result = await _sut.GetSecretAsync(getDto);

        result.Should().Be("super-secret-password");
    }

    [Fact]
    public async Task RotateSecret_ChangesEncryptedValue()
    {
        await SeedDefinition();

        var setDto = new SetSecretDto
        {
            DefinitionKey = "app.secret.key",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "original-password"
        };
        await _sut.SetSecretAsync(setDto, "test-user");

        var originalEncrypted = (await _db.EncryptedSecrets.FirstAsync()).EncryptedValue;

        var rotateDto = new RotateSecretDto
        {
            DefinitionKey = "app.secret.key",
            ScopeType = ScopeType.Machine,
            NewPlaintextValue = "rotated-password"
        };
        await _sut.RotateSecretAsync(rotateDto, "test-user");

        var rotatedEncrypted = (await _db.EncryptedSecrets.FirstAsync()).EncryptedValue;
        rotatedEncrypted.Should().NotBe(originalEncrypted,
            "rotating a secret should produce a different encrypted value");

        // Verify the rotated value decrypts correctly
        var getDto = new GetSecretDto
        {
            DefinitionKey = "app.secret.key",
            ScopeType = ScopeType.Machine
        };
        var decrypted = await _sut.GetSecretAsync(getDto);
        decrypted.Should().Be("rotated-password");
    }

    [Fact]
    public async Task SetSecret_OnNonSecretDefinition_ThrowsInvalidOperationException()
    {
        await SeedDefinition("app.nonsecret.key", isSecret: false);

        var dto = new SetSecretDto
        {
            DefinitionKey = "app.nonsecret.key",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "value"
        };

        var act = () => _sut.SetSecretAsync(dto, "test-user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a secret*");
    }

    [Fact]
    public async Task SetSecret_OnNonExistentDefinition_ThrowsKeyNotFoundException()
    {
        var dto = new SetSecretDto
        {
            DefinitionKey = "nonexistent.key",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "value"
        };

        var act = () => _sut.SetSecretAsync(dto, "test-user");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task SetSecret_CreatesAuditEvent()
    {
        await SeedDefinition();

        var dto = new SetSecretDto
        {
            DefinitionKey = "app.secret.key",
            ScopeType = ScopeType.Machine,
            PlaintextValue = "secret-value"
        };

        await _sut.SetSecretAsync(dto, "test-user");

        _auditMock.Verify(
            a => a.RecordAsync(
                It.Is<AuditEventDto>(e =>
                    e.EventType == AuditEventType.SecretRotated &&
                    e.DefinitionKey == "app.secret.key" &&
                    e.ActorId == "test-user"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

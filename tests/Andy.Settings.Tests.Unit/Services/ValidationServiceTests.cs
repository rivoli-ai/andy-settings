using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Services;
using FluentAssertions;

namespace Andy.Settings.Tests.Unit.Services;

public class ValidationServiceTests
{
    private readonly ValidationService _sut = new();

    private static SettingDefinition MakeDefinition(SettingDataType dataType) => new()
    {
        Id = Guid.NewGuid(),
        Key = "test.key",
        ApplicationCode = "testapp",
        DisplayName = "Test",
        DataType = dataType,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void ValidateString_ValidValue_ReturnsNull()
    {
        var def = MakeDefinition(SettingDataType.String);
        var result = _sut.ValidateValue(def, "\"hello world\"");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateString_InvalidValue_ReturnsError()
    {
        var def = MakeDefinition(SettingDataType.String);
        var result = _sut.ValidateValue(def, "42");
        result.Should().NotBeNull();
        result.Should().Contain("string");
    }

    [Fact]
    public void ValidateInteger_ValidValue_ReturnsNull()
    {
        var def = MakeDefinition(SettingDataType.Integer);
        var result = _sut.ValidateValue(def, "42");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateInteger_InvalidValue_ReturnsError()
    {
        var def = MakeDefinition(SettingDataType.Integer);
        var result = _sut.ValidateValue(def, "\"not-a-number\"");
        result.Should().NotBeNull();
        result.Should().Contain("integer");
    }

    [Fact]
    public void ValidateBoolean_TrueValue_ReturnsNull()
    {
        var def = MakeDefinition(SettingDataType.Boolean);
        var result = _sut.ValidateValue(def, "true");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateBoolean_FalseValue_ReturnsNull()
    {
        var def = MakeDefinition(SettingDataType.Boolean);
        var result = _sut.ValidateValue(def, "false");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateBoolean_InvalidValue_ReturnsError()
    {
        var def = MakeDefinition(SettingDataType.Boolean);
        var result = _sut.ValidateValue(def, "\"yes\"");
        result.Should().NotBeNull();
        result.Should().Contain("boolean");
    }

    [Fact]
    public void ValidateUri_ValidValue_ReturnsNull()
    {
        var def = MakeDefinition(SettingDataType.Uri);
        var result = _sut.ValidateValue(def, "\"https://example.com/path\"");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateUri_InvalidValue_ReturnsError()
    {
        var def = MakeDefinition(SettingDataType.Uri);
        var result = _sut.ValidateValue(def, "\"not a uri\"");
        result.Should().NotBeNull();
        result.Should().Contain("URI");
    }

    [Fact]
    public void ValidateJson_ValidObject_ReturnsNull()
    {
        var def = MakeDefinition(SettingDataType.Json);
        var result = _sut.ValidateValue(def, "{\"key\": \"value\"}");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateJson_InvalidJson_ReturnsError()
    {
        var def = MakeDefinition(SettingDataType.Json);
        var result = _sut.ValidateValue(def, "{invalid json}");
        result.Should().NotBeNull();
        result.Should().Contain("JSON");
    }

    [Fact]
    public void ValidateSecret_ReturnsNull()
    {
        var def = MakeDefinition(SettingDataType.Secret);
        var result = _sut.ValidateValue(def, "\"any-secret-value\"");
        result.Should().BeNull();
    }
}

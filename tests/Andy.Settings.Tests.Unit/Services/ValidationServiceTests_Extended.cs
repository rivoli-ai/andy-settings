using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Services;
using FluentAssertions;

namespace Andy.Settings.Tests.Unit.Services;

public class ValidationServiceTests_Extended
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
    public void ValidateDecimal_ValidNumber_ReturnsNull()
    {
        var def = MakeDefinition(SettingDataType.Decimal);
        var result = _sut.ValidateValue(def, "3.14");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateDecimal_InvalidString_ReturnsError()
    {
        var def = MakeDefinition(SettingDataType.Decimal);
        var result = _sut.ValidateValue(def, "\"not-a-number\"");
        result.Should().NotBeNull();
        result.Should().Contain("number");
    }

    [Fact]
    public void ValidateDuration_AnyValue_ReturnsNull()
    {
        var def = MakeDefinition(SettingDataType.Duration);
        var result = _sut.ValidateValue(def, "\"00:05:00\"");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateEnum_AnyValue_ReturnsNull()
    {
        var def = MakeDefinition(SettingDataType.Enum);
        var result = _sut.ValidateValue(def, "\"OptionA\"");
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateStringList_AnyValue_ReturnsNull()
    {
        var def = MakeDefinition(SettingDataType.StringList);
        var result = _sut.ValidateValue(def, "[\"a\",\"b\",\"c\"]");
        result.Should().BeNull();
    }
}

using System.Text.Json;
using Andy.Settings.Domain.Enums;
using FluentAssertions;

namespace Andy.Settings.Tests.Unit.Domain;

public class SettingDataTypeTests
{
    [Theory]
    [InlineData(SettingDataType.String, "String")]
    [InlineData(SettingDataType.Integer, "Integer")]
    [InlineData(SettingDataType.Boolean, "Boolean")]
    [InlineData(SettingDataType.Decimal, "Decimal")]
    [InlineData(SettingDataType.Enum, "Enum")]
    [InlineData(SettingDataType.Duration, "Duration")]
    [InlineData(SettingDataType.Uri, "Uri")]
    [InlineData(SettingDataType.Json, "Json")]
    [InlineData(SettingDataType.StringList, "StringList")]
    [InlineData(SettingDataType.Secret, "Secret")]
    public void SettingDataType_SerializesAsString(SettingDataType dataType, string expected)
    {
        var json = JsonSerializer.Serialize(dataType);
        json.Should().Be($"\"{expected}\"");
    }

    [Fact]
    public void SettingDataType_DeserializesFromString()
    {
        var result = JsonSerializer.Deserialize<SettingDataType>("\"Integer\"");
        result.Should().Be(SettingDataType.Integer);
    }

    [Fact]
    public void SettingDataType_HasExpectedNumberOfValues()
    {
        var values = System.Enum.GetValues<SettingDataType>();
        values.Should().HaveCount(10);
    }
}

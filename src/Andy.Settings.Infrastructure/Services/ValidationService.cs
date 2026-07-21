using System.Text.Json;
using System.Text.RegularExpressions;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Infrastructure.Services;

public class ValidationService : IValidationService
{
    public string? ValidateValue(SettingDefinition definition, string valueJson)
    {
        try
        {
            using var valueDocument = JsonDocument.Parse(valueJson);
            var value = valueDocument.RootElement;
            var typeError = definition.DataType switch
            {
                SettingDataType.String => RequireKind(value, JsonValueKind.String, "JSON string"),
                SettingDataType.Integer => value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out _)
                    ? "Expected a JSON integer value." : null,
                SettingDataType.Boolean => value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                    ? "Expected a JSON boolean value." : null,
                SettingDataType.Decimal => RequireKind(value, JsonValueKind.Number, "JSON number"),
                SettingDataType.Enum => RequireKind(value, JsonValueKind.String, "JSON string enum value"),
                SettingDataType.Duration => ValidateDuration(value),
                SettingDataType.Uri => ValidateUri(value),
                SettingDataType.Json => null,
                SettingDataType.StringList => ValidateStringList(value),
                SettingDataType.Secret => null,
                _ => "Unsupported setting data type."
            };

            return typeError ?? ValidateSchema(value, definition.ValidationJson);
        }
        catch (JsonException ex)
        {
            return $"Invalid JSON: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Validation error: {ex.Message}";
        }
    }

    private static string? RequireKind(JsonElement value, JsonValueKind kind, string expected) =>
        value.ValueKind == kind ? null : $"Expected a {expected} value.";

    private static string? ValidateDuration(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
            return "Expected a JSON string containing a duration.";
        return TimeSpan.TryParse(value.GetString(), out _)
            ? null : $"Invalid duration: '{value.GetString()}'.";
    }

    private static string? ValidateUri(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
            return "Expected a JSON string value containing a URI.";
        return Uri.TryCreate(value.GetString(), UriKind.Absolute, out _)
            ? null : $"Invalid URI: '{value.GetString()}'.";
    }

    private static string? ValidateStringList(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            return "Expected a JSON array of strings.";
        return value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String)
            ? null : "Expected every list item to be a JSON string.";
    }

    // Supports the validation keywords currently emitted by registration
    // manifests without pulling a full schema engine into the hot write path.
    private static string? ValidateSchema(JsonElement value, string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
            return null;

        using var schemaDocument = JsonDocument.Parse(schemaJson);
        var schema = schemaDocument.RootElement;
        if (schema.ValueKind != JsonValueKind.Object)
            return "ValidationJson must be a JSON object.";

        if (schema.TryGetProperty("enum", out var allowed) && allowed.ValueKind == JsonValueKind.Array &&
            !allowed.EnumerateArray().Any(candidate => candidate.GetRawText() == value.GetRawText()))
            return "Value is not one of the allowed enum values.";

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString() ?? string.Empty;
            if (schema.TryGetProperty("minLength", out var minLength) && text.Length < minLength.GetInt32())
                return $"String must contain at least {minLength.GetInt32()} characters.";
            if (schema.TryGetProperty("maxLength", out var maxLength) && text.Length > maxLength.GetInt32())
                return $"String must contain at most {maxLength.GetInt32()} characters.";
            if (schema.TryGetProperty("pattern", out var pattern) &&
                !Regex.IsMatch(text, pattern.GetString() ?? string.Empty, RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(250)))
                return "String does not match the required pattern.";
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            if (schema.TryGetProperty("minimum", out var minimum) && number < minimum.GetDecimal())
                return $"Number must be at least {minimum.GetDecimal()}.";
            if (schema.TryGetProperty("maximum", out var maximum) && number > maximum.GetDecimal())
                return $"Number must be at most {maximum.GetDecimal()}.";
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var count = value.GetArrayLength();
            if (schema.TryGetProperty("minItems", out var minItems) && count < minItems.GetInt32())
                return $"Array must contain at least {minItems.GetInt32()} items.";
            if (schema.TryGetProperty("maxItems", out var maxItems) && count > maxItems.GetInt32())
                return $"Array must contain at most {maxItems.GetInt32()} items.";
        }

        return null;
    }
}

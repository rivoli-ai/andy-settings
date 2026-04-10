using System.Text.Json;
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
            return definition.DataType switch
            {
                SettingDataType.String => ValidateString(valueJson),
                SettingDataType.Integer => ValidateInteger(valueJson),
                SettingDataType.Boolean => ValidateBoolean(valueJson),
                SettingDataType.Decimal => ValidateDecimal(valueJson),
                SettingDataType.Uri => ValidateUri(valueJson),
                SettingDataType.Json => ValidateJson(valueJson),
                SettingDataType.Secret => null, // secrets are validated by the secret service
                _ => null
            };
        }
        catch (Exception ex)
        {
            return $"Validation error: {ex.Message}";
        }
    }

    private static string? ValidateString(string valueJson)
    {
        var doc = JsonDocument.Parse(valueJson);
        return doc.RootElement.ValueKind != JsonValueKind.String
            ? "Expected a JSON string value."
            : null;
    }

    private static string? ValidateInteger(string valueJson)
    {
        var doc = JsonDocument.Parse(valueJson);
        return doc.RootElement.ValueKind != JsonValueKind.Number || !doc.RootElement.TryGetInt64(out _)
            ? "Expected a JSON integer value."
            : null;
    }

    private static string? ValidateBoolean(string valueJson)
    {
        var doc = JsonDocument.Parse(valueJson);
        return doc.RootElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            ? "Expected a JSON boolean value."
            : null;
    }

    private static string? ValidateDecimal(string valueJson)
    {
        var doc = JsonDocument.Parse(valueJson);
        return doc.RootElement.ValueKind != JsonValueKind.Number
            ? "Expected a JSON number value."
            : null;
    }

    private static string? ValidateUri(string valueJson)
    {
        var doc = JsonDocument.Parse(valueJson);
        if (doc.RootElement.ValueKind != JsonValueKind.String)
            return "Expected a JSON string value containing a URI.";

        var value = doc.RootElement.GetString();
        return !System.Uri.TryCreate(value, UriKind.Absolute, out _)
            ? $"Invalid URI: '{value}'."
            : null;
    }

    private static string? ValidateJson(string valueJson)
    {
        try
        {
            JsonDocument.Parse(valueJson);
            return null;
        }
        catch (JsonException)
        {
            return "Invalid JSON.";
        }
    }
}

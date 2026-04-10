using Andy.Settings.Domain.Entities;

namespace Andy.Settings.Application.Interfaces;

public interface IValidationService
{
    /// <summary>
    /// Validates a value against a setting definition's data type and schema.
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    string? ValidateValue(SettingDefinition definition, string valueJson);
}

// Copyright (c) Rivoli AI 2026. All rights reserved.
using System.Text.Json.Serialization;

namespace Andy.Settings.Api.Data;

/// <summary>
/// Wire format of config/registration.json files produced by the andy-service-template.
/// Andy Settings reads the settings.definitions section on startup and inserts
/// matching SettingDefinition rows.
///
/// Schema: ../../andy-service-template/docs/registration.schema.json
/// </summary>
public sealed record RegistrationManifest(
    [property: JsonPropertyName("service")]  RegistrationServiceInfo Service,
    [property: JsonPropertyName("settings")] RegistrationSettingsInfo? Settings
);

public sealed record RegistrationServiceInfo(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description
);

public sealed record RegistrationSettingsInfo(
    [property: JsonPropertyName("definitions")] RegistrationSettingDefinition[]? Definitions
);

public sealed record RegistrationSettingDefinition(
    [property: JsonPropertyName("key")]           string Key,
    [property: JsonPropertyName("displayName")]   string? DisplayName,
    [property: JsonPropertyName("description")]   string? Description,
    [property: JsonPropertyName("category")]      string? Category,
    [property: JsonPropertyName("dataType")]      string DataType,
    [property: JsonPropertyName("defaultValue")]  object? DefaultValue,
    [property: JsonPropertyName("isSecret")]      bool? IsSecret,
    [property: JsonPropertyName("allowedScopes")] string[]? AllowedScopes
);

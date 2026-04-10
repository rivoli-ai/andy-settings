// Andy Settings API - C# Example
// Usage: ANDY_SETTINGS_URL=http://localhost:5399 dotnet run

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var baseUrl = Environment.GetEnvironmentVariable("ANDY_SETTINGS_URL") ?? "https://localhost:5300";
var token = Environment.GetEnvironmentVariable("ANDY_SETTINGS_TOKEN") ?? "";

// Skip SSL validation for local development
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
};

using var http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
if (!string.IsNullOrEmpty(token))
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

// 1. List definitions
Console.WriteLine("=== List Definitions ===");
var definitions = await http.GetFromJsonAsync<JsonElement>("/api/definitions");
Console.WriteLine($"Total: {definitions.GetProperty("totalCount")}");

// 2. Resolve effective value
Console.WriteLine("\n=== Resolve Effective Value ===");
var resolveRequest = new { key = "andy.containers.defaultProvider", context = new { applicationCode = "containers", userId = "user-123" } };
var resolveResponse = await http.PostAsJsonAsync("/api/effective/resolve", resolveRequest);
var resolved = await resolveResponse.Content.ReadFromJsonAsync<JsonElement>();
Console.WriteLine($"Effective: {resolved.GetProperty("effectiveValue")}, isDefault: {resolved.GetProperty("isDefault")}");

// 3. Set a value
Console.WriteLine("\n=== Set Value ===");
var setValue = new { definitionKey = "andy.containers.defaultProvider", scopeType = "User", scopeId = "user-123", valueJson = "\"docker\"" };
var setResponse = await http.PostAsJsonAsync("/api/values", setValue);
Console.WriteLine($"Set value: {setResponse.StatusCode}");

// 4. Explain resolution
Console.WriteLine("\n=== Explain Resolution ===");
var explainResponse = await http.PostAsJsonAsync("/api/effective/explain", resolveRequest);
var explanation = await explainResponse.Content.ReadFromJsonAsync<JsonElement>();
foreach (var entry in explanation.GetProperty("sourceChain").EnumerateArray())
    Console.WriteLine($"  {entry.GetProperty("scopeType")}: {entry.GetProperty("valueJson")} {(entry.GetProperty("isWinner").GetBoolean() ? "<-- WINNER" : "")}");

// 5. Export settings
Console.WriteLine("\n=== Export Settings ===");
var export = await http.GetFromJsonAsync<JsonElement>("/api/export?applicationCode=containers");
Console.WriteLine($"Exported {export.GetProperty("definitionCount")} definitions");

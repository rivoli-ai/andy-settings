// Andy Settings API - C# Example
// Usage: dotnet run

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var baseUrl = "https://localhost:5300";
var token = Environment.GetEnvironmentVariable("ANDY_SETTINGS_TOKEN") ?? "your-jwt-token";

using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

// Skip SSL validation for local development
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
};

// 1. List definitions
Console.WriteLine("=== List Definitions ===");
var definitions = await http.GetFromJsonAsync<JsonElement>("/api/definitions");
Console.WriteLine(JsonSerializer.Serialize(definitions, new JsonSerializerOptions { WriteIndented = true }));

// 2. Resolve effective value
Console.WriteLine("\n=== Resolve Effective Value ===");
var resolveRequest = new
{
    key = "andy.containers.defaultProvider",
    context = new
    {
        applicationCode = "containers",
        userId = "user-123"
    }
};
var resolveResponse = await http.PostAsJsonAsync("/api/effective/resolve", resolveRequest);
var resolved = await resolveResponse.Content.ReadFromJsonAsync<JsonElement>();
Console.WriteLine(JsonSerializer.Serialize(resolved, new JsonSerializerOptions { WriteIndented = true }));

// 3. Set a value
Console.WriteLine("\n=== Set Value ===");
var setValue = new
{
    definitionKey = "andy.containers.defaultProvider",
    scopeType = "User",
    scopeId = "user-123",
    valueJson = "\"docker\""
};
var setResponse = await http.PostAsJsonAsync("/api/values", setValue);
Console.WriteLine($"Set value: {setResponse.StatusCode}");

// 4. Explain resolution
Console.WriteLine("\n=== Explain Resolution ===");
var explainResponse = await http.PostAsJsonAsync("/api/effective/explain", resolveRequest);
var explanation = await explainResponse.Content.ReadFromJsonAsync<JsonElement>();
Console.WriteLine(JsonSerializer.Serialize(explanation, new JsonSerializerOptions { WriteIndented = true }));

// 5. Export settings
Console.WriteLine("\n=== Export Settings ===");
var export = await http.GetFromJsonAsync<JsonElement>("/api/export?applicationCode=containers");
Console.WriteLine(JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }));

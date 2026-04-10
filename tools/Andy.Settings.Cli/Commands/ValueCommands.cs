using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace Andy.Settings.Cli.Commands;

public static class ValueCommands
{
    public static Command BuildGetCommand(Option<string> apiUrlOption, Option<string> formatOption)
    {
        var getCommand = new Command("get", "Get the effective value of a setting");
        var keyArg = new Argument<string>("key", "The setting key");
        var scopeOption = new Option<string?>("--scope", "Scope (e.g. Machine, User, Team)");
        var scopeIdOption = new Option<string?>("--scope-id", "Scope identifier");
        var userOption = new Option<string?>("--user", "User identifier");
        var teamOption = new Option<string?>("--team", "Team identifier");
        var appOption = new Option<string?>("--app", "Application identifier");

        getCommand.AddArgument(keyArg);
        getCommand.AddOption(scopeOption);
        getCommand.AddOption(scopeIdOption);
        getCommand.AddOption(userOption);
        getCommand.AddOption(teamOption);
        getCommand.AddOption(appOption);

        getCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var format = ctx.ParseResult.GetValueForOption(formatOption)!;
            var key = ctx.ParseResult.GetValueForArgument(keyArg);
            var scope = ctx.ParseResult.GetValueForOption(scopeOption);
            var scopeId = ctx.ParseResult.GetValueForOption(scopeIdOption);
            var user = ctx.ParseResult.GetValueForOption(userOption);
            var team = ctx.ParseResult.GetValueForOption(teamOption);
            var app = ctx.ParseResult.GetValueForOption(appOption);

            using var client = HttpClientFactory.Create(apiUrl);

            var payload = new Dictionary<string, object?> { ["key"] = key };
            if (!string.IsNullOrEmpty(scope)) payload["scope"] = scope;
            if (!string.IsNullOrEmpty(scopeId)) payload["scopeId"] = scopeId;
            if (!string.IsNullOrEmpty(user)) payload["user"] = user;
            if (!string.IsNullOrEmpty(team)) payload["team"] = team;
            if (!string.IsNullOrEmpty(app)) payload["app"] = app;

            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("api/effective/resolve", content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    return;
                }

                if (format == "json")
                {
                    Console.WriteLine(body);
                    return;
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("value", out var valueEl))
                {
                    Console.WriteLine(valueEl.ToString());
                }
                else
                {
                    Console.WriteLine(body);
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        return getCommand;
    }

    public static Command BuildSetCommand(Option<string> apiUrlOption)
    {
        var setCommand = new Command("set", "Set a setting value");
        var keyArg = new Argument<string>("key", "The setting key");
        var valueArg = new Argument<string>("value", "The value to set");
        var scopeOption = new Option<string>("--scope", getDefaultValue: () => "Machine", description: "Scope (default: Machine)");
        var scopeIdOption = new Option<string?>("--scope-id", "Scope identifier");

        setCommand.AddArgument(keyArg);
        setCommand.AddArgument(valueArg);
        setCommand.AddOption(scopeOption);
        setCommand.AddOption(scopeIdOption);

        setCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var key = ctx.ParseResult.GetValueForArgument(keyArg);
            var value = ctx.ParseResult.GetValueForArgument(valueArg);
            var scope = ctx.ParseResult.GetValueForOption(scopeOption)!;
            var scopeId = ctx.ParseResult.GetValueForOption(scopeIdOption);

            using var client = HttpClientFactory.Create(apiUrl);

            var payload = new Dictionary<string, object?>
            {
                ["key"] = key,
                ["value"] = value,
                ["scope"] = scope
            };
            if (!string.IsNullOrEmpty(scopeId)) payload["scopeId"] = scopeId;

            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("api/values", content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    return;
                }

                Console.WriteLine($"Setting '{key}' set successfully.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        return setCommand;
    }

    public static Command BuildExplainCommand(Option<string> apiUrlOption, Option<string> formatOption)
    {
        var explainCommand = new Command("explain", "Explain how a setting value is resolved");
        var keyArg = new Argument<string>("key", "The setting key");
        var userOption = new Option<string?>("--user", "User identifier");
        var teamOption = new Option<string?>("--team", "Team identifier");
        var appOption = new Option<string?>("--app", "Application identifier");

        explainCommand.AddArgument(keyArg);
        explainCommand.AddOption(userOption);
        explainCommand.AddOption(teamOption);
        explainCommand.AddOption(appOption);

        explainCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var format = ctx.ParseResult.GetValueForOption(formatOption)!;
            var key = ctx.ParseResult.GetValueForArgument(keyArg);
            var user = ctx.ParseResult.GetValueForOption(userOption);
            var team = ctx.ParseResult.GetValueForOption(teamOption);
            var app = ctx.ParseResult.GetValueForOption(appOption);

            using var client = HttpClientFactory.Create(apiUrl);

            var payload = new Dictionary<string, object?> { ["key"] = key };
            if (!string.IsNullOrEmpty(user)) payload["user"] = user;
            if (!string.IsNullOrEmpty(team)) payload["team"] = team;
            if (!string.IsNullOrEmpty(app)) payload["app"] = app;

            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("api/effective/explain", content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    return;
                }

                if (format == "json")
                {
                    Console.WriteLine(body);
                    return;
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Display effective value
                if (root.TryGetProperty("effectiveValue", out var effectiveVal))
                {
                    Console.WriteLine($"Effective value: {effectiveVal}");
                    Console.WriteLine();
                }

                // Display source chain as table
                if (root.TryGetProperty("sources", out var sources) &&
                    sources.ValueKind == JsonValueKind.Array)
                {
                    var table = new Table();
                    table.Border(TableBorder.Rounded);
                    table.AddColumn("Priority");
                    table.AddColumn("Scope");
                    table.AddColumn("ScopeId");
                    table.AddColumn("Value");
                    table.AddColumn("Source");

                    var priority = 1;
                    foreach (var source in sources.EnumerateArray())
                    {
                        table.AddRow(
                            priority.ToString(),
                            source.TryGetProperty("scope", out var sc) ? sc.GetString() ?? "" : "",
                            source.TryGetProperty("scopeId", out var sid) ? sid.GetString() ?? "" : "",
                            source.TryGetProperty("value", out var v) ? v.ToString() : "",
                            source.TryGetProperty("source", out var sr) ? sr.GetString() ?? "" : ""
                        );
                        priority++;
                    }

                    AnsiConsole.Write(table);
                }
                else
                {
                    // Fallback: just print the body
                    Console.WriteLine(body);
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        return explainCommand;
    }
}

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

    public static Command BuildDeleteCommand(Option<string> apiUrlOption)
    {
        var deleteCommand = new Command("delete", "Delete a setting value assignment");
        var keyArg = new Argument<string>("key", "The setting key");
        var scopeOption = new Option<string?>("--scope", "Scope (e.g. Machine, User, Team)");
        var scopeIdOption = new Option<string?>("--scope-id", "Scope identifier");

        deleteCommand.AddArgument(keyArg);
        deleteCommand.AddOption(scopeOption);
        deleteCommand.AddOption(scopeIdOption);

        deleteCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var key = ctx.ParseResult.GetValueForArgument(keyArg);
            var scope = ctx.ParseResult.GetValueForOption(scopeOption);
            var scopeId = ctx.ParseResult.GetValueForOption(scopeIdOption);

            using var client = HttpClientFactory.Create(apiUrl);

            try
            {
                // First, find the assignment ID
                var queryParams = new List<string> { $"definitionKey={Uri.EscapeDataString(key)}" };
                if (!string.IsNullOrEmpty(scope)) queryParams.Add($"scopeType={Uri.EscapeDataString(scope)}");
                if (!string.IsNullOrEmpty(scopeId)) queryParams.Add($"scopeId={Uri.EscapeDataString(scopeId)}");

                var lookupUrl = "api/values?" + string.Join("&", queryParams);
                var lookupResponse = await client.GetAsync(lookupUrl);
                var lookupBody = await lookupResponse.Content.ReadAsStringAsync();

                if (!lookupResponse.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)lookupResponse.StatusCode}: {lookupBody}");
                    return;
                }

                using var doc = JsonDocument.Parse(lookupBody);
                var items = doc.RootElement.EnumerateArray();
                string? assignmentId = null;

                foreach (var item in items)
                {
                    if (item.TryGetProperty("id", out var idEl))
                    {
                        assignmentId = idEl.ToString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(assignmentId))
                {
                    Console.Error.WriteLine("No matching value assignment found.");
                    return;
                }

                // Now delete it
                var deleteResponse = await client.DeleteAsync($"api/values/{assignmentId}");
                var deleteBody = await deleteResponse.Content.ReadAsStringAsync();

                if (!deleteResponse.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)deleteResponse.StatusCode}: {deleteBody}");
                    return;
                }

                Console.WriteLine($"Setting '{key}' deleted successfully.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        return deleteCommand;
    }

    public static Command BuildListCommand(Option<string> apiUrlOption, Option<string> formatOption)
    {
        var valuesCommand = new Command("values", "Manage setting values");

        var listCommand = new Command("list", "List setting value assignments");
        var keyOption = new Option<string?>("--key", "Filter by definition key");
        var scopeOption = new Option<string?>("--scope", "Filter by scope type");
        var scopeIdOption = new Option<string?>("--scope-id", "Filter by scope identifier");
        var pageOption = new Option<int>("--page", getDefaultValue: () => 1, description: "Page number");
        var pageSizeOption = new Option<int>("--page-size", getDefaultValue: () => 20, description: "Page size");

        listCommand.AddOption(keyOption);
        listCommand.AddOption(scopeOption);
        listCommand.AddOption(scopeIdOption);
        listCommand.AddOption(pageOption);
        listCommand.AddOption(pageSizeOption);

        listCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var format = ctx.ParseResult.GetValueForOption(formatOption)!;
            var key = ctx.ParseResult.GetValueForOption(keyOption);
            var scope = ctx.ParseResult.GetValueForOption(scopeOption);
            var scopeId = ctx.ParseResult.GetValueForOption(scopeIdOption);
            var page = ctx.ParseResult.GetValueForOption(pageOption);
            var pageSize = ctx.ParseResult.GetValueForOption(pageSizeOption);

            using var client = HttpClientFactory.Create(apiUrl);

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(key)) queryParams.Add($"definitionKey={Uri.EscapeDataString(key)}");
            if (!string.IsNullOrEmpty(scope)) queryParams.Add($"scopeType={Uri.EscapeDataString(scope)}");
            if (!string.IsNullOrEmpty(scopeId)) queryParams.Add($"scopeId={Uri.EscapeDataString(scopeId)}");
            queryParams.Add($"page={page}");
            queryParams.Add($"pageSize={pageSize}");

            var url = "api/values?" + string.Join("&", queryParams);

            try
            {
                var response = await client.GetAsync(url);
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
                var items = doc.RootElement.EnumerateArray();

                var table = new Table();
                table.Border(TableBorder.Rounded);
                table.AddColumn("DefinitionKey");
                table.AddColumn("ScopeType");
                table.AddColumn("ScopeId");
                table.AddColumn("Value");
                table.AddColumn("Version");

                foreach (var item in items)
                {
                    table.AddRow(
                        item.TryGetProperty("definitionKey", out var dk) ? dk.GetString() ?? "" : "",
                        item.TryGetProperty("scopeType", out var st) ? st.GetString() ?? "" : "",
                        item.TryGetProperty("scopeId", out var si) ? si.GetString() ?? "" : "",
                        item.TryGetProperty("value", out var v) ? v.ToString() : "",
                        item.TryGetProperty("version", out var ver) ? ver.ToString() : ""
                    );
                }

                AnsiConsole.Write(table);
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        valuesCommand.AddCommand(listCommand);

        return valuesCommand;
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

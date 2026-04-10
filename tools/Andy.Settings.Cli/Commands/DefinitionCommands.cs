using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace Andy.Settings.Cli.Commands;

public static class DefinitionCommands
{
    public static Command Build(Option<string> apiUrlOption, Option<string> formatOption)
    {
        var definitionsCommand = new Command("definitions", "Manage setting definitions");

        // --- definitions list ---
        var listCommand = new Command("list", "List setting definitions");

        var appOption = new Option<string?>("--app", "Filter by application");
        var categoryOption = new Option<string?>("--category", "Filter by category");
        var pageOption = new Option<int>("--page", getDefaultValue: () => 1, description: "Page number");
        var pageSizeOption = new Option<int>("--page-size", getDefaultValue: () => 20, description: "Page size");

        listCommand.AddOption(appOption);
        listCommand.AddOption(categoryOption);
        listCommand.AddOption(pageOption);
        listCommand.AddOption(pageSizeOption);

        listCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var format = ctx.ParseResult.GetValueForOption(formatOption)!;
            var app = ctx.ParseResult.GetValueForOption(appOption);
            var category = ctx.ParseResult.GetValueForOption(categoryOption);
            var page = ctx.ParseResult.GetValueForOption(pageOption);
            var pageSize = ctx.ParseResult.GetValueForOption(pageSizeOption);

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(app)) queryParams.Add($"app={Uri.EscapeDataString(app)}");
            if (!string.IsNullOrEmpty(category)) queryParams.Add($"category={Uri.EscapeDataString(category)}");
            queryParams.Add($"page={page}");
            queryParams.Add($"pageSize={pageSize}");

            var url = "api/definitions?" + string.Join("&", queryParams);

            await FetchAndDisplayDefinitions(apiUrl, url, format);
        });

        // --- definitions search ---
        var searchCommand = new Command("search", "Search setting definitions");
        var queryArg = new Argument<string>("query", "Search query");
        searchCommand.AddArgument(queryArg);

        searchCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var format = ctx.ParseResult.GetValueForOption(formatOption)!;
            var query = ctx.ParseResult.GetValueForArgument(queryArg);

            var url = $"api/definitions?search={Uri.EscapeDataString(query)}";

            await FetchAndDisplayDefinitions(apiUrl, url, format);
        });

        // --- definitions get ---
        var getCommand = new Command("get", "Get a setting definition by key");
        var getKeyArg = new Argument<string>("key", "The definition key");
        getCommand.AddArgument(getKeyArg);

        getCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var format = ctx.ParseResult.GetValueForOption(formatOption)!;
            var key = ctx.ParseResult.GetValueForArgument(getKeyArg);

            using var client = HttpClientFactory.Create(apiUrl);

            try
            {
                var response = await client.GetAsync($"api/definitions/{Uri.EscapeDataString(key)}");
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

                foreach (var prop in root.EnumerateObject())
                {
                    Console.WriteLine($"{prop.Name}: {prop.Value}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        // --- definitions create ---
        var createCommand = new Command("create", "Create a new setting definition");
        var createKeyOption = new Option<string>("--key", "The definition key") { IsRequired = true };
        var createAppOption = new Option<string?>("--app", "Application identifier");
        var createNameOption = new Option<string?>("--name", "Display name");
        var createTypeOption = new Option<string>("--type", getDefaultValue: () => "String", description: "Data type");
        var createCategoryOption = new Option<string?>("--category", "Category");
        var createDescriptionOption = new Option<string?>("--description", "Description");
        var createIsSecretOption = new Option<bool>("--is-secret", getDefaultValue: () => false, description: "Whether the setting is a secret");

        createCommand.AddOption(createKeyOption);
        createCommand.AddOption(createAppOption);
        createCommand.AddOption(createNameOption);
        createCommand.AddOption(createTypeOption);
        createCommand.AddOption(createCategoryOption);
        createCommand.AddOption(createDescriptionOption);
        createCommand.AddOption(createIsSecretOption);

        createCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var key = ctx.ParseResult.GetValueForOption(createKeyOption)!;
            var app = ctx.ParseResult.GetValueForOption(createAppOption);
            var name = ctx.ParseResult.GetValueForOption(createNameOption);
            var type = ctx.ParseResult.GetValueForOption(createTypeOption)!;
            var category = ctx.ParseResult.GetValueForOption(createCategoryOption);
            var description = ctx.ParseResult.GetValueForOption(createDescriptionOption);
            var isSecret = ctx.ParseResult.GetValueForOption(createIsSecretOption);

            using var client = HttpClientFactory.Create(apiUrl);

            var payload = new Dictionary<string, object?>
            {
                ["key"] = key,
                ["dataType"] = type,
                ["isSecret"] = isSecret
            };
            if (!string.IsNullOrEmpty(app)) payload["app"] = app;
            if (!string.IsNullOrEmpty(name)) payload["displayName"] = name;
            if (!string.IsNullOrEmpty(category)) payload["category"] = category;
            if (!string.IsNullOrEmpty(description)) payload["description"] = description;

            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("api/definitions", content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    return;
                }

                Console.WriteLine($"Definition '{key}' created successfully.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        // --- definitions update ---
        var updateCommand = new Command("update", "Update a setting definition");
        var updateKeyArg = new Argument<string>("key", "The definition key");
        var updateNameOption = new Option<string?>("--name", "Display name");
        var updateCategoryOption = new Option<string?>("--category", "Category");
        var updateDescriptionOption = new Option<string?>("--description", "Description");
        var updateDeprecatedOption = new Option<bool?>("--deprecated", "Mark as deprecated");

        updateCommand.AddArgument(updateKeyArg);
        updateCommand.AddOption(updateNameOption);
        updateCommand.AddOption(updateCategoryOption);
        updateCommand.AddOption(updateDescriptionOption);
        updateCommand.AddOption(updateDeprecatedOption);

        updateCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var key = ctx.ParseResult.GetValueForArgument(updateKeyArg);
            var name = ctx.ParseResult.GetValueForOption(updateNameOption);
            var category = ctx.ParseResult.GetValueForOption(updateCategoryOption);
            var description = ctx.ParseResult.GetValueForOption(updateDescriptionOption);
            var deprecated = ctx.ParseResult.GetValueForOption(updateDeprecatedOption);

            using var client = HttpClientFactory.Create(apiUrl);

            var payload = new Dictionary<string, object?>();
            if (!string.IsNullOrEmpty(name)) payload["displayName"] = name;
            if (!string.IsNullOrEmpty(category)) payload["category"] = category;
            if (!string.IsNullOrEmpty(description)) payload["description"] = description;
            if (deprecated.HasValue) payload["deprecated"] = deprecated.Value;

            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PutAsync($"api/definitions/{Uri.EscapeDataString(key)}", content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    return;
                }

                Console.WriteLine($"Definition '{key}' updated successfully.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        // --- definitions delete ---
        var deleteCommand = new Command("delete", "Delete a setting definition");
        var deleteKeyArg = new Argument<string>("key", "The definition key");
        deleteCommand.AddArgument(deleteKeyArg);

        deleteCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var key = ctx.ParseResult.GetValueForArgument(deleteKeyArg);

            using var client = HttpClientFactory.Create(apiUrl);

            try
            {
                var response = await client.DeleteAsync($"api/definitions/{Uri.EscapeDataString(key)}");
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    return;
                }

                Console.WriteLine($"Definition '{key}' deleted successfully.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        definitionsCommand.AddCommand(listCommand);
        definitionsCommand.AddCommand(searchCommand);
        definitionsCommand.AddCommand(getCommand);
        definitionsCommand.AddCommand(createCommand);
        definitionsCommand.AddCommand(updateCommand);
        definitionsCommand.AddCommand(deleteCommand);

        return definitionsCommand;
    }

    private static async Task FetchAndDisplayDefinitions(string apiUrl, string url, string format)
    {
        using var client = HttpClientFactory.Create(apiUrl);

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
            table.AddColumn("Key");
            table.AddColumn("App");
            table.AddColumn("DisplayName");
            table.AddColumn("DataType");
            table.AddColumn("Category");
            table.AddColumn("IsSecret");

            foreach (var item in items)
            {
                table.AddRow(
                    item.GetProperty("key").GetString() ?? "",
                    item.TryGetProperty("app", out var appVal) ? appVal.GetString() ?? "" : "",
                    item.TryGetProperty("displayName", out var dnVal) ? dnVal.GetString() ?? "" : "",
                    item.TryGetProperty("dataType", out var dtVal) ? dtVal.GetString() ?? "" : "",
                    item.TryGetProperty("category", out var catVal) ? catVal.GetString() ?? "" : "",
                    item.TryGetProperty("isSecret", out var secVal) ? secVal.GetBoolean().ToString() : "False"
                );
            }

            AnsiConsole.Write(table);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Connection error: {ex.Message}");
        }
    }
}

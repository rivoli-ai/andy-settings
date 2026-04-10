using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http;
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

        definitionsCommand.AddCommand(listCommand);
        definitionsCommand.AddCommand(searchCommand);

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

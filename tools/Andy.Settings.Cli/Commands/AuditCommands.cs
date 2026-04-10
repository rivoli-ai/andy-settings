using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http;
using System.Text.Json;
using Spectre.Console;

namespace Andy.Settings.Cli.Commands;

public static class AuditCommands
{
    public static Command Build(Option<string> apiUrlOption, Option<string> formatOption)
    {
        var auditCommand = new Command("audit", "View audit log entries");

        var keyOption = new Option<string?>("--key", "Filter by definition key");
        var pageOption = new Option<int>("--page", getDefaultValue: () => 1, description: "Page number");
        var pageSizeOption = new Option<int>("--page-size", getDefaultValue: () => 20, description: "Page size");

        auditCommand.AddOption(keyOption);
        auditCommand.AddOption(pageOption);
        auditCommand.AddOption(pageSizeOption);

        auditCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var format = ctx.ParseResult.GetValueForOption(formatOption)!;
            var key = ctx.ParseResult.GetValueForOption(keyOption);
            var page = ctx.ParseResult.GetValueForOption(pageOption);
            var pageSize = ctx.ParseResult.GetValueForOption(pageSizeOption);

            using var client = HttpClientFactory.Create(apiUrl);

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(key)) queryParams.Add($"key={Uri.EscapeDataString(key)}");
            queryParams.Add($"page={page}");
            queryParams.Add($"pageSize={pageSize}");

            var url = "api/audit?" + string.Join("&", queryParams);

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
                table.AddColumn("EventType");
                table.AddColumn("DefinitionKey");
                table.AddColumn("ScopeType");
                table.AddColumn("ActorId");
                table.AddColumn("CreatedAt");

                foreach (var item in items)
                {
                    table.AddRow(
                        item.TryGetProperty("eventType", out var et) ? et.GetString() ?? "" : "",
                        item.TryGetProperty("definitionKey", out var dk) ? dk.GetString() ?? "" : "",
                        item.TryGetProperty("scopeType", out var st) ? st.GetString() ?? "" : "",
                        item.TryGetProperty("actorId", out var ai) ? ai.GetString() ?? "" : "",
                        item.TryGetProperty("createdAt", out var ca) ? ca.GetString() ?? "" : ""
                    );
                }

                AnsiConsole.Write(table);
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        return auditCommand;
    }
}

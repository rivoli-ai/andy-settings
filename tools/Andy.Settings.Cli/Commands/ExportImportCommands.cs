using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace Andy.Settings.Cli.Commands;

public static class ExportImportCommands
{
    public static Command BuildExportCommand(Option<string> apiUrlOption)
    {
        var exportCommand = new Command("export", "Export settings");
        var appOption = new Option<string?>("--app", "Filter by application");
        var formatOption = new Option<string>("--format", getDefaultValue: () => "json", description: "Export format");
        var outputOption = new Option<string?>("--output", "Output file path (default: stdout)");

        exportCommand.AddOption(appOption);
        exportCommand.AddOption(formatOption);
        exportCommand.AddOption(outputOption);

        exportCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var app = ctx.ParseResult.GetValueForOption(appOption);
            var format = ctx.ParseResult.GetValueForOption(formatOption)!;
            var output = ctx.ParseResult.GetValueForOption(outputOption);

            using var client = HttpClientFactory.Create(apiUrl);

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(app)) queryParams.Add($"app={Uri.EscapeDataString(app)}");
            if (!string.IsNullOrEmpty(format)) queryParams.Add($"format={Uri.EscapeDataString(format)}");

            var url = "api/export";
            if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

            try
            {
                var response = await client.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    return;
                }

                if (!string.IsNullOrEmpty(output))
                {
                    await File.WriteAllTextAsync(output, body);
                    Console.WriteLine($"Exported to {output}");
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

        return exportCommand;
    }

    public static Command BuildImportCommand(Option<string> apiUrlOption)
    {
        var importCommand = new Command("import", "Import settings from a file");
        var fileArg = new Argument<string>("file", "Path to the import file");
        var previewOption = new Option<bool>("--preview", "Preview import without applying changes");

        importCommand.AddArgument(fileArg);
        importCommand.AddOption(previewOption);

        importCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var file = ctx.ParseResult.GetValueForArgument(fileArg);
            var preview = ctx.ParseResult.GetValueForOption(previewOption);

            if (!File.Exists(file))
            {
                Console.Error.WriteLine($"File not found: {file}");
                return;
            }

            using var client = HttpClientFactory.Create(apiUrl);

            try
            {
                var fileContent = await File.ReadAllTextAsync(file);
                var content = new StringContent(fileContent, Encoding.UTF8, "application/json");

                var url = preview ? "api/import/preview" : "api/import";
                var response = await client.PostAsync(url, content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    return;
                }

                if (preview)
                {
                    Console.WriteLine("Import preview:");
                    Console.WriteLine();

                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var formatted = JsonSerializer.Serialize(doc.RootElement,
                            new JsonSerializerOptions { WriteIndented = true });
                        Console.WriteLine(formatted);
                    }
                    catch
                    {
                        Console.WriteLine(body);
                    }
                }
                else
                {
                    Console.WriteLine("Import completed successfully.");

                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        var formatted = JsonSerializer.Serialize(doc.RootElement,
                            new JsonSerializerOptions { WriteIndented = true });
                        Console.WriteLine(formatted);
                    }
                    catch
                    {
                        Console.WriteLine(body);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        return importCommand;
    }
}

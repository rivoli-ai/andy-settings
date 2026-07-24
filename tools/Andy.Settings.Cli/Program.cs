using System.CommandLine;
using Andy.Settings.Cli;
using Andy.Settings.Cli.Commands;

var rootCommand = new RootCommand("Andy Settings CLI - Manage application settings and configuration");

// Global options
var apiUrlOption = new Option<string>(
    "--api-url",
    getDefaultValue: () => "https://localhost:5300",
    description: "The Andy Settings API base URL");
rootCommand.AddGlobalOption(apiUrlOption);

var formatOption = new Option<string>(
    "--format",
    getDefaultValue: () => "table",
    description: "Output format (table or json)");
formatOption.FromAmong("table", "json");
rootCommand.AddGlobalOption(formatOption);

// Certificate validation is on by default — this CLI carries bearer tokens
// and plaintext secrets. The bypass has to be asked for explicitly
// (rivoli-ai/andy-settings#129).
var insecureOption = new Option<bool>(
    "--insecure",
    description: "Skip TLS certificate validation. For local development against "
               + "self-signed certificates only — never against a production host.");
rootCommand.AddGlobalOption(insecureOption);

if (rootCommand.Parse(args).GetValueForOption(insecureOption)
    || HttpClientFactory.InsecureRequestedViaEnvironment())
{
    HttpClientFactory.AllowInsecureTls();
}

// Auth commands (auth login, auth logout)
rootCommand.AddCommand(AuthCommands.Build());

// Definition commands (definitions list, definitions search)
rootCommand.AddCommand(DefinitionCommands.Build(apiUrlOption, formatOption));

// Value commands (get, set, explain, delete, values list)
rootCommand.AddCommand(ValueCommands.BuildGetCommand(apiUrlOption, formatOption));
rootCommand.AddCommand(ValueCommands.BuildSetCommand(apiUrlOption));
rootCommand.AddCommand(ValueCommands.BuildExplainCommand(apiUrlOption, formatOption));
rootCommand.AddCommand(ValueCommands.BuildDeleteCommand(apiUrlOption));
rootCommand.AddCommand(ValueCommands.BuildListCommand(apiUrlOption, formatOption));

// Secret commands (secrets set, get, rotate, delete)
rootCommand.AddCommand(SecretCommands.Build(apiUrlOption));

// Audit commands (audit)
rootCommand.AddCommand(AuditCommands.Build(apiUrlOption, formatOption));

// Export / Import commands
rootCommand.AddCommand(ExportImportCommands.BuildExportCommand(apiUrlOption));
rootCommand.AddCommand(ExportImportCommands.BuildImportCommand(apiUrlOption));

return await rootCommand.InvokeAsync(args);

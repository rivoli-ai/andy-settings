using System.CommandLine;

var rootCommand = new RootCommand("Andy Settings CLI - Manage application settings and configuration");

var apiUrlOption = new Option<string>(
    "--api-url",
    getDefaultValue: () => "https://localhost:5300",
    description: "The Andy Settings API base URL");
rootCommand.AddGlobalOption(apiUrlOption);

// TODO: Add command groups (see stories for implementation order)
// - auth (login, logout)
// - settings (list, get, set, delete, history)
// - environments (list, create, delete)
// - export / import

return await rootCommand.InvokeAsync(args);

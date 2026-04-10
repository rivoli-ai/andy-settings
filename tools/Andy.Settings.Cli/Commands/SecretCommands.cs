using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Andy.Settings.Cli.Commands;

public static class SecretCommands
{
    public static Command Build(Option<string> apiUrlOption)
    {
        var secretsCommand = new Command("secrets", "Manage secrets");

        // --- secrets set ---
        var setCommand = new Command("set", "Set a secret value");
        var setKeyArg = new Argument<string>("key", "The secret key");
        var setValueArg = new Argument<string>("value", "The secret value");
        var setScopeOption = new Option<string?>("--scope", "Scope (e.g. Machine, User, Team)");
        var setScopeIdOption = new Option<string?>("--scope-id", "Scope identifier");

        setCommand.AddArgument(setKeyArg);
        setCommand.AddArgument(setValueArg);
        setCommand.AddOption(setScopeOption);
        setCommand.AddOption(setScopeIdOption);

        setCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var key = ctx.ParseResult.GetValueForArgument(setKeyArg);
            var value = ctx.ParseResult.GetValueForArgument(setValueArg);
            var scope = ctx.ParseResult.GetValueForOption(setScopeOption);
            var scopeId = ctx.ParseResult.GetValueForOption(setScopeIdOption);

            using var client = HttpClientFactory.Create(apiUrl);

            var payload = new Dictionary<string, object?> { ["value"] = value };
            if (!string.IsNullOrEmpty(scope)) payload["scopeType"] = scope;
            if (!string.IsNullOrEmpty(scopeId)) payload["scopeId"] = scopeId;

            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"api/secrets/{Uri.EscapeDataString(key)}", content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    return;
                }

                Console.WriteLine($"Secret '{key}' set successfully.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        // --- secrets get ---
        var getCommand = new Command("get", "Get a secret value");
        var getKeyArg = new Argument<string>("key", "The secret key");
        var getScopeOption = new Option<string?>("--scope", "Scope (e.g. Machine, User, Team)");
        var getScopeIdOption = new Option<string?>("--scope-id", "Scope identifier");

        getCommand.AddArgument(getKeyArg);
        getCommand.AddOption(getScopeOption);
        getCommand.AddOption(getScopeIdOption);

        getCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var key = ctx.ParseResult.GetValueForArgument(getKeyArg);
            var scope = ctx.ParseResult.GetValueForOption(getScopeOption);
            var scopeId = ctx.ParseResult.GetValueForOption(getScopeIdOption);

            using var client = HttpClientFactory.Create(apiUrl);

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(scope)) queryParams.Add($"scopeType={Uri.EscapeDataString(scope)}");
            if (!string.IsNullOrEmpty(scopeId)) queryParams.Add($"scopeId={Uri.EscapeDataString(scopeId)}");

            var url = $"api/secrets/{Uri.EscapeDataString(key)}";
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

                Console.WriteLine(body);
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        // --- secrets rotate ---
        var rotateCommand = new Command("rotate", "Rotate a secret value");
        var rotateKeyArg = new Argument<string>("key", "The secret key");
        var rotateValueArg = new Argument<string>("new-value", "The new secret value");
        var rotateScopeOption = new Option<string?>("--scope", "Scope (e.g. Machine, User, Team)");
        var rotateScopeIdOption = new Option<string?>("--scope-id", "Scope identifier");

        rotateCommand.AddArgument(rotateKeyArg);
        rotateCommand.AddArgument(rotateValueArg);
        rotateCommand.AddOption(rotateScopeOption);
        rotateCommand.AddOption(rotateScopeIdOption);

        rotateCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var key = ctx.ParseResult.GetValueForArgument(rotateKeyArg);
            var newValue = ctx.ParseResult.GetValueForArgument(rotateValueArg);
            var scope = ctx.ParseResult.GetValueForOption(rotateScopeOption);
            var scopeId = ctx.ParseResult.GetValueForOption(rotateScopeIdOption);

            using var client = HttpClientFactory.Create(apiUrl);

            var payload = new Dictionary<string, object?> { ["value"] = newValue };
            if (!string.IsNullOrEmpty(scope)) payload["scopeType"] = scope;
            if (!string.IsNullOrEmpty(scopeId)) payload["scopeId"] = scopeId;

            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"api/secrets/{Uri.EscapeDataString(key)}/rotate", content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    return;
                }

                Console.WriteLine($"Secret '{key}' rotated successfully.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        // --- secrets delete ---
        var deleteCommand = new Command("delete", "Delete a secret");
        var deleteKeyArg = new Argument<string>("key", "The secret key");
        deleteCommand.AddArgument(deleteKeyArg);

        deleteCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var key = ctx.ParseResult.GetValueForArgument(deleteKeyArg);

            using var client = HttpClientFactory.Create(apiUrl);

            try
            {
                var response = await client.DeleteAsync($"api/secrets/{Uri.EscapeDataString(key)}");
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    return;
                }

                Console.WriteLine($"Secret '{key}' deleted successfully.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
            }
        });

        secretsCommand.AddCommand(setCommand);
        secretsCommand.AddCommand(getCommand);
        secretsCommand.AddCommand(rotateCommand);
        secretsCommand.AddCommand(deleteCommand);

        return secretsCommand;
    }
}

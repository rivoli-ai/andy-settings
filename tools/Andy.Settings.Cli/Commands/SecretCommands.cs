using System.CommandLine;
using Andy.Settings.Cli;
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
        // Optional. Passing a secret on the command line puts it in shell
        // history and in `ps` output for the lifetime of the process, readable
        // by any local user (rivoli-ai/andy-settings#141). Omit it and the
        // value is read from stdin, or prompted for without echo.
        var setValueArg = new Argument<string?>("value",
            () => null,
            "The secret value. Omit to read from stdin, or to be prompted without echo.");
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
            var value = SecretInput.Resolve(
                ctx.ParseResult.GetValueForArgument(setValueArg),
                "Secret value: ");
            if (value is null)
            {
                Console.Error.WriteLine("No secret value provided.");
                ctx.ExitCode = 1;
                return;
            }
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
                    ctx.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"Secret '{key}' set successfully.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
                ctx.ExitCode = 1;
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
                    ctx.ExitCode = 1;
                    return;
                }

                Console.WriteLine(body);
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        // --- secrets rotate ---
        var rotateCommand = new Command("rotate", "Rotate a secret value");
        var rotateKeyArg = new Argument<string>("key", "The secret key");
        var rotateValueArg = new Argument<string?>("new-value",
            () => null,
            "The new secret value. Omit to read from stdin, or to be prompted without echo.");
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
            var newValue = SecretInput.Resolve(
                ctx.ParseResult.GetValueForArgument(rotateValueArg),
                "New secret value: ");
            if (newValue is null)
            {
                Console.Error.WriteLine("No secret value provided.");
                ctx.ExitCode = 1;
                return;
            }
            var scope = ctx.ParseResult.GetValueForOption(rotateScopeOption);
            var scopeId = ctx.ParseResult.GetValueForOption(rotateScopeIdOption);

            using var client = HttpClientFactory.Create(apiUrl);

            // The rotate endpoint binds RotateSecretBody, whose property is
            // `newValue` — not `value`. Sending `value` left NewValue null on
            // the server, so `secrets rotate` never worked.
            var payload = new Dictionary<string, object?> { ["newValue"] = newValue };
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
                    ctx.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"Secret '{key}' rotated successfully.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        // --- secrets delete ---
        // Deletion is per-scope. `--all-scopes` is required to wipe every
        // scope, so clearing one user's credential cannot silently take out
        // the machine-scope value and every other user's
        // (rivoli-ai/andy-settings#138).
        var deleteCommand = new Command("delete", "Delete a secret at a scope, or every scope with --all-scopes");
        var deleteKeyArg = new Argument<string>("key", "The secret key");
        var deleteScopeOption = new Option<string?>("--scope", "Scope (e.g. Machine, User, Team)");
        var deleteScopeIdOption = new Option<string?>("--scope-id", "Scope identifier");
        var deleteAllScopesOption = new Option<bool>("--all-scopes",
            "Delete every stored secret for this definition, across all scopes");
        deleteCommand.AddArgument(deleteKeyArg);
        deleteCommand.AddOption(deleteScopeOption);
        deleteCommand.AddOption(deleteScopeIdOption);
        deleteCommand.AddOption(deleteAllScopesOption);

        deleteCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var apiUrl = ctx.ParseResult.GetValueForOption(apiUrlOption)!;
            var key = ctx.ParseResult.GetValueForArgument(deleteKeyArg);
            var scope = ctx.ParseResult.GetValueForOption(deleteScopeOption);
            var scopeId = ctx.ParseResult.GetValueForOption(deleteScopeIdOption);
            var allScopes = ctx.ParseResult.GetValueForOption(deleteAllScopesOption);

            if (string.IsNullOrEmpty(scope) && !allScopes)
            {
                Console.Error.WriteLine(
                    "Specify --scope (and --scope-id for non-Machine scopes), "
                    + "or pass --all-scopes to delete every stored secret for this definition.");
                ctx.ExitCode = 1;
                return;
            }

            using var client = HttpClientFactory.Create(apiUrl);

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(scope)) queryParams.Add($"scopeType={Uri.EscapeDataString(scope)}");
            if (!string.IsNullOrEmpty(scopeId)) queryParams.Add($"scopeId={Uri.EscapeDataString(scopeId)}");
            if (allScopes) queryParams.Add("allScopes=true");

            var url = $"api/secrets/{Uri.EscapeDataString(key)}";
            if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

            try
            {
                var response = await client.DeleteAsync(url);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Error {(int)response.StatusCode}: {body}");
                    ctx.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"Secret '{key}' deleted successfully.");
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Connection error: {ex.Message}");
                ctx.ExitCode = 1;
            }
        });

        secretsCommand.AddCommand(setCommand);
        secretsCommand.AddCommand(getCommand);
        secretsCommand.AddCommand(rotateCommand);
        secretsCommand.AddCommand(deleteCommand);

        return secretsCommand;
    }
}

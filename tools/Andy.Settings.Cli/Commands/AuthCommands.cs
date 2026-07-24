using System.CommandLine;
using System.CommandLine.Invocation;

namespace Andy.Settings.Cli.Commands;

// OAuth Device Flow is NOT implemented. It was advertised as a shipped feature
// in the README and five documents, while `auth login` was a stub and `auth
// logout` printed "Token cleared." despite there being no token store to clear
// (rivoli-ai/andy-settings#141, docs corrected in #146).
//
// These commands are kept — rather than removed — so that anyone following the
// old documentation gets an actionable message instead of "unrecognized
// command". They report accurately and `login` exits non-zero, because it
// cannot do what its name promises.
public static class AuthCommands
{
    private const string TokenEnvVar = "ANDY_SETTINGS_TOKEN";

    public static Command Build()
    {
        var authCommand = new Command("auth", "Authentication commands");

        var loginCommand = new Command("login", $"Not implemented — set {TokenEnvVar} instead");
        loginCommand.SetHandler((InvocationContext ctx) =>
        {
            Console.Error.WriteLine(
                $"OAuth device flow is not implemented. Authenticate by setting the {TokenEnvVar} "
                + "environment variable to a bearer token for the Andy Settings API:");
            Console.Error.WriteLine();
            Console.Error.WriteLine($"    export {TokenEnvVar}=<token>");
            ctx.ExitCode = 1;
        });

        var logoutCommand = new Command("logout", $"Explains how to clear {TokenEnvVar}");
        logoutCommand.SetHandler(() =>
        {
            // There is no token store, so there is nothing to clear. Saying
            // "Token cleared." was simply untrue.
            var tokenSet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TokenEnvVar));
            Console.WriteLine(tokenSet
                ? $"This CLI keeps no token store. {TokenEnvVar} is currently set; "
                  + $"unset it to log out:\n\n    unset {TokenEnvVar}"
                : $"This CLI keeps no token store, and {TokenEnvVar} is not set. Nothing to do.");
        });

        authCommand.AddCommand(loginCommand);
        authCommand.AddCommand(logoutCommand);

        return authCommand;
    }
}

using System.CommandLine;

namespace Andy.Settings.Cli.Commands;

public static class AuthCommands
{
    public static Command Build()
    {
        var authCommand = new Command("auth", "Authentication commands");

        var loginCommand = new Command("login", "Log in to Andy Settings");
        loginCommand.SetHandler(() =>
        {
            Console.WriteLine(
                "OAuth Device Flow not yet implemented. Set ANDY_SETTINGS_TOKEN env var.");
        });

        var logoutCommand = new Command("logout", "Log out of Andy Settings");
        logoutCommand.SetHandler(() =>
        {
            Console.WriteLine("Token cleared.");
        });

        authCommand.AddCommand(loginCommand);
        authCommand.AddCommand(logoutCommand);

        return authCommand;
    }
}

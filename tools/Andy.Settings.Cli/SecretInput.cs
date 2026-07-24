using System;
using System.Text;

namespace Andy.Settings.Cli;

/// <summary>
/// Reads a secret value without putting it on the command line.
/// </summary>
/// <remarks>
/// A value passed as an argv element lands in shell history and is visible in
/// <c>ps</c> output for the lifetime of the process, readable by any local
/// user (rivoli-ai/andy-settings#141). The positional argument still works so
/// existing scripts keep running, but it is now optional: omit it and the
/// value is read from a pipe, or prompted for with echo suppressed.
/// </remarks>
public static class SecretInput
{
    /// <summary>
    /// Resolves a secret value from, in order: the supplied argument, piped
    /// stdin, or an interactive prompt. Returns null when nothing could be
    /// read.
    /// </summary>
    public static string? Resolve(string? argumentValue, string prompt)
    {
        if (!string.IsNullOrEmpty(argumentValue))
            return argumentValue;

        // Piped or redirected input: read it all, so a value containing spaces
        // or newlines survives. Trailing newline from `echo` is stripped.
        if (Console.IsInputRedirected)
        {
            var piped = Console.In.ReadToEnd();
            return piped.TrimEnd('\r', '\n') is { Length: > 0 } value ? value : null;
        }

        return ReadHidden(prompt);
    }

    private static string? ReadHidden(string prompt)
    {
        Console.Error.Write(prompt);

        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.Error.WriteLine();
                    return builder.Length > 0 ? builder.ToString() : null;

                case ConsoleKey.Backspace when builder.Length > 0:
                    builder.Length--;
                    continue;

                case ConsoleKey.Backspace:
                    continue;

                case ConsoleKey.Escape:
                    Console.Error.WriteLine();
                    return null;

                default:
                    // Control characters other than those handled above are not
                    // part of the value.
                    if (!char.IsControl(key.KeyChar))
                        builder.Append(key.KeyChar);
                    continue;
            }
        }
    }
}

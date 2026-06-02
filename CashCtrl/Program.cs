using CashCtrl.Screens;
using CashCtrl.Services;
using Spectre.Console;

Console.Title  = "Cash-Ctrl";
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Disable cursor for a cleaner TUI feel
try { Console.CursorVisible = false; } catch { }

var cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

try
{
    if (cliArgs.Length == 0)
    {
        // ── cash-ctrl  →  main welcome menu ─────────────────────────────────
        await WelcomeScreen.ShowAsync();
    }
    else if (cliArgs[0] == ".")
    {
        // ── cash-ctrl .  →  browse current directory ─────────────────────────
        await WelcomeScreen.ShowLocalBrowseAsync(Directory.GetCurrentDirectory());
    }
    else
    {
        // ── cash-ctrl <name>  →  open or create a specific control ─────────
        var controlArg  = cliArgs[0];
        var searchPath  = Path.Combine(Directory.GetCurrentDirectory(), controlArg + ".json");
        var exactPath   = Path.IsPathRooted(controlArg) && File.Exists(controlArg)
                          ? controlArg : null;
        var resolvedPath = exactPath ?? searchPath;

        if (File.Exists(resolvedPath))
        {
            await OpenControlScreen.ShowAsync(resolvedPath);
        }
        else
        {
            await CreateControlScreen.ShowAsync(controlArg, Directory.GetCurrentDirectory());
        }
    }
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    AnsiConsole.Clear();
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
    Console.ReadKey(true);
}
finally
{
    try { Console.CursorVisible = true; } catch { }
}

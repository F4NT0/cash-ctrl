using CashCtrl;
using CashCtrl.Screens;
using CashCtrl.Services;
using Spectre.Console;

Console.Title  = "Cash-Ctrl";
Console.OutputEncoding = System.Text.Encoding.UTF8;

// Disable cursor for a cleaner TUI feel
try { Console.CursorVisible = false; } catch { }

var cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

// Kick off remote version check in background (non-blocking)
CashCtrl.Services.VersionService.StartBackgroundCheck();

try
{
    if (cliArgs.Length == 0)
    {
        // ── cash-ctrl  →  main welcome menu ─────────────────────────────────
        await Task.Delay(400); // give background version check a head start
        await WelcomeScreen.ShowAsync();
    }
    else if (cliArgs[0] is "--version" or "-v")
    {
        // ── cash-ctrl --version  →  print version ───────────────────────────
        var p = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var m = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        AnsiConsole.MarkupLine($"[bold {p}]Cash-Ctrl[/] [bold white]{AppVersion.Current}[/]");
        AnsiConsole.MarkupLine($"[{m}]Love you Lili [/][red]\u2665[/]");
        AnsiConsole.MarkupLine($"[{m}]Run cash-ctrl --help for more information[/]");
    }
    else if (cliArgs[0] is "--update" or "update")
    {
        // ── cash-ctrl --update  →  download + install latest release ────────
        await VersionService.PerformUpdateAsync();
    }
    else if (cliArgs[0] is "--help" or "-h" or "help")
    {
        // ── cash-ctrl --help  →  print usage ────────────────────────────────
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        AnsiConsole.Clear();
        var p = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var s = $"#{Theme.Secondary.R:X2}{Theme.Secondary.G:X2}{Theme.Secondary.B:X2}";
        var m = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var a = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        AnsiConsole.MarkupLine($"[bold {p}]Cash-Ctrl[/] [bold {s}]— Terminal-native personal finance control[/]");
        Console.WriteLine();
        AnsiConsole.MarkupLine($"[bold {a}]USAGE[/]");
        AnsiConsole.MarkupLine($"  [{p}]cash-ctrl[/]               [{m}]Open the main welcome menu[/]");
        AnsiConsole.MarkupLine($"  [{p}]cash-ctrl[/] [{s}]<name>[/]        [{m}]Open or create a control by name in the current directory[/]");
        AnsiConsole.MarkupLine($"  [{p}]cash-ctrl[/] [{s}].[/]             [{m}]Browse controls in the current directory[/]");
        AnsiConsole.MarkupLine($"  [{p}]cash-ctrl[/] [{s}]--install[/]     [{m}]Run the TUI installer (add cash-ctrl to PATH)[/]");
        AnsiConsole.MarkupLine($"  [{p}]cash-ctrl[/] [{s}]--uninstall[/]   [{m}]Remove cash-ctrl from PATH and disk[/]");
        AnsiConsole.MarkupLine($"  [{p}]cash-ctrl[/] [{s}]--update[/]      [{m}]Download and install the latest version[/]");
        AnsiConsole.MarkupLine($"  [{p}]cash-ctrl[/] [{s}]--version[/]     [{m}]Show version information[/]");
        AnsiConsole.MarkupLine($"  [{p}]cash-ctrl[/] [{s}]--help[/]        [{m}]Show this help message[/]");
        Console.WriteLine();
        AnsiConsole.MarkupLine($"[bold {a}]EXAMPLES[/]");
        AnsiConsole.MarkupLine($"  [{m}]cash-ctrl[/]               [{m}]# opens main menu[/]");
        AnsiConsole.MarkupLine($"  [{m}]cash-ctrl Fevereiro-2026[/] [{m}]# opens or creates Fevereiro-2026.json[/]");
        AnsiConsole.MarkupLine($"  [{m}]cash-ctrl .[/]             [{m}]# browse current directory[/]");
        Console.WriteLine();
        AnsiConsole.MarkupLine($"[bold {a}]DATA[/]");
        AnsiConsole.MarkupLine($"  [{m}]Controls are stored as JSON files (.json) in the directory where you run the command.[/]");
        AnsiConsole.MarkupLine($"  [{m}]Favorites/recents are stored in:[/] [{s}]%APPDATA%\\CashCtrl\\favorites.json[/]");
        Console.WriteLine();
    }
    else if (cliArgs[0] is "--install" or "install")
    {
        // ── cash-ctrl --install  →  TUI installer ───────────────────────────
        await InstallerScreen.ShowAsync();
    }
    else if (cliArgs[0] is "--uninstall" or "uninstall")
    {
        // ── cash-ctrl --uninstall  →  remove cash-ctrl from PATH and disk ───
        await InstallerScreen.UninstallAsync();
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
    try { AnsiConsole.Clear(); } catch { }
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
    try { Console.ReadKey(true); } catch { }
}
finally
{
    try { Console.CursorVisible = true; } catch { }
}

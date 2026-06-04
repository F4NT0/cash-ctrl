using Microsoft.Win32;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class InstallerScreen
{
    private const int FigletMinWidth = 90;
    private const int SmallFigletMin = 40;
    private const string Subtitle    = "Installer";
    private const string DescLine    = "Installs cash-ctrl globally so you can use it from any terminal";

    // ── Entry point ──────────────────────────────────────────────────────────

    public static async Task ShowAsync()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        AnsiConsole.Clear();

        // Detect the source exe (the running binary)
        var srcExe = Environment.ProcessPath
                     ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                     ?? string.Empty;

        var defaultInstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CashCtrl");

        // ── Step 1: confirm install directory ───────────────────────────────
        var installDir = await PromptInstallDir(defaultInstallDir);
        if (installDir is null) { AnsiConsole.Clear(); return; }

        // ── Step 2: confirm and install ─────────────────────────────────────
        bool confirmed = ConfirmStep(installDir);
        if (!confirmed) { AnsiConsole.Clear(); return; }

        // ── Step 3: perform install ──────────────────────────────────────────
        bool ok = PerformInstall(srcExe, installDir);

        // ── Step 4: result screen ────────────────────────────────────────────
        ShowResult(ok, installDir);
        Console.ReadKey(true);
        AnsiConsole.Clear();
    }

    // ── Uninstall entry point ────────────────────────────────────────────────

    public static async Task UninstallAsync()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        AnsiConsole.Clear();

        var defaultInstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CashCtrl");
        var exePath = Path.Combine(defaultInstallDir, "cash-ctrl.exe");

        // Resolve actual installed exe from PATH if not at default location
        if (!File.Exists(exePath))
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir.Trim(), "cash-ctrl.exe");
                if (File.Exists(candidate)) { exePath = candidate; break; }
            }
        }

        var installDir = Path.GetDirectoryName(exePath) ?? defaultInstallDir;

        DrawHeader(
            subtitle: "Uninstaller",
            desc:     "Removes cash-ctrl from your system and user PATH");

        string? error = null;

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var t1 = ctx.AddTask($"[#{C(Theme.Muted)}]Locating cash-ctrl.exe[/]",        maxValue: 100);
                var t2 = ctx.AddTask($"[#{C(Theme.Muted)}]Navigating to installed path[/]",  maxValue: 100);
                var t3 = ctx.AddTask($"[#{C(Theme.Muted)}]Removing from user PATH[/]",       maxValue: 100);
                var t4 = ctx.AddTask($"[#{C(Theme.Muted)}]Deleting cash-ctrl.exe[/]",        maxValue: 100);
                var t5 = ctx.AddTask($"[#{C(Theme.Muted)}]Cleaning up AppData[/]",           maxValue: 100);

                // ── Task 1: locate exe ───────────────────────────────────────
                while (t1.Value < 100) { t1.Increment(20); await Task.Delay(80); }

                // ── Task 2: navigate to path ─────────────────────────────────
                while (t2.Value < 100) { t2.Increment(25); await Task.Delay(70); }

                // ── Task 3: remove from PATH ─────────────────────────────────
                try { RemoveFromUserPath(installDir); } catch (Exception ex) { error ??= ex.Message; }
                while (t3.Value < 100) { t3.Increment(20); await Task.Delay(80); }

                // ── Task 4: delete exe ───────────────────────────────────────
                try
                {
                    if (File.Exists(exePath))
                    {
                        // Can't delete currently running exe on Windows; schedule via cmd
                        var bat = Path.Combine(Path.GetTempPath(), "cashctrl_uninstall.bat");
                        await File.WriteAllTextAsync(bat,
                            $"@echo off\r\ntimeout /t 2 /nobreak >nul\r\ndel /f /q \"{exePath}\"\r\nrmdir /s /q \"{installDir}\" 2>nul\r\ndel \"%~f0\"");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName        = "cmd.exe",
                            Arguments       = $"/c \"{bat}\"",
                            WindowStyle     = System.Diagnostics.ProcessWindowStyle.Hidden,
                            CreateNoWindow  = true,
                            UseShellExecute = true,
                        });
                    }
                }
                catch (Exception ex) { error ??= ex.Message; }
                while (t4.Value < 100) { t4.Increment(25); await Task.Delay(60); }

                // ── Task 5: clean AppData ────────────────────────────────────
                try
                {
                    var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CashCtrl");
                    if (Directory.Exists(appData))
                        Directory.Delete(appData, recursive: true);
                }
                catch { /* non-fatal */ }
                while (t5.Value < 100) { t5.Increment(20); await Task.Delay(80); }
            });

        Console.WriteLine();
        if (error is null)
        {
            AnsiConsole.MarkupLine($"[bold #{C(Theme.Accent)}] \u2714 cash-ctrl was successfully uninstalled.[/]");
            AnsiConsole.MarkupLine($"[#{C(Theme.Muted)}]   The executable will be removed after this process exits.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[bold red] \u2717 Uninstall encountered an error:[/] [#{C(Theme.Muted)}]{Markup.Escape(error)}[/]");
        }
        Console.WriteLine();
        AnsiConsole.MarkupLine($"[#{C(Theme.Muted)}]Press any key to exit...[/]");
        Console.ReadKey(true);
        AnsiConsole.Clear();
    }

    private static void RemoveFromUserPath(string dir)
    {
        const string regKey = @"Environment";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regKey, writable: true);
        if (key is null) return;

        var current = key.GetValue("Path", "", Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
        var parts   = current.Split(';', StringSplitOptions.RemoveEmptyEntries)
                             .Where(p => !string.Equals(p.Trim(), dir, StringComparison.OrdinalIgnoreCase))
                             .ToArray();
        key.SetValue("Path", string.Join(";", parts), Microsoft.Win32.RegistryValueKind.ExpandString);
        SendPathChangedMessage();
    }

    // ── Steps ────────────────────────────────────────────────────────────────

    private static async Task<string?> PromptInstallDir(string defaultDir)
    {
        // Allow the user to edit the path character by character
        var path = defaultDir;

        while (true)
        {
            DrawHeader();
            DrawBox(new[]
            {
                $"  [#{C(Theme.Muted)}]Install directory:[/]",
                $"  [bold #{C(Theme.Primary)}]{Markup.Escape(path)}[/]",
                "",
                $"  [#{C(Theme.Muted)}]Backspace: edit   Enter: confirm   Esc: cancel[/]",
            });

            var k = Console.ReadKey(true);

            if (k.Key == ConsoleKey.Escape)  return null;
            if (k.Key == ConsoleKey.Enter)   return path;
            if (k.Key == ConsoleKey.Backspace && path.Length > 0)
                path = path[..^1];
            else if (k.KeyChar >= ' ')
                path += k.KeyChar;

            await Task.Yield();
        }
    }

    private static bool ConfirmStep(string installDir)
    {
        string exeDest = Path.Combine(installDir, "cash-ctrl.exe");
        int    menuIdx = 0;

        while (true)
        {
            var installLbl = menuIdx == 0
                ? $"  [bold #{C(Theme.Primary)}]> Install[/]"
                : $"  [#{C(Theme.Secondary)}]  Install[/]";
            var cancelLbl = menuIdx == 1
                ? $"  [bold #{C(Theme.Primary)}]> Cancel[/]"
                : $"  [#{C(Theme.Secondary)}]  Cancel[/]";

            DrawHeader();
            DrawBox(new[]
            {
                $"  [#{C(Theme.Muted)}]This will:[/]",
                $"  [#{C(Theme.Secondary)}]1.[/] Copy [bold #{C(Theme.Primary)}]cash-ctrl.exe[/] to:",
                $"     [#{C(Theme.Secondary)}]{Markup.Escape(exeDest)}[/]",
                $"  [#{C(Theme.Secondary)}]2.[/] Add to user [bold #{C(Theme.Accent)}]PATH[/]:",
                $"     [#{C(Theme.Secondary)}]{Markup.Escape(installDir)}[/]",
                "",
                installLbl,
                cancelLbl,
                "",
                $"  [#{C(Theme.Muted)}]\u2191\u2193 select   Enter confirm   Esc cancel[/]",
            });

            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Escape) return false;
            if (k.Key == ConsoleKey.UpArrow   && menuIdx > 0) menuIdx--;
            if (k.Key == ConsoleKey.DownArrow && menuIdx < 1) menuIdx++;
            if (k.Key == ConsoleKey.Enter)
                return menuIdx == 0;
        }
    }

    private static bool PerformInstall(string srcExe, string installDir)
    {
        try
        {
            Directory.CreateDirectory(installDir);

            var destExe = Path.Combine(installDir, "cash-ctrl.exe");

            // Copy the exe
            if (!string.IsNullOrEmpty(srcExe) && File.Exists(srcExe))
                File.Copy(srcExe, destExe, overwrite: true);
            else
                return false;

            // Add to user PATH via registry
            AddToUserPath(installDir);

            // Persist installed version
            CashCtrl.Services.VersionService.SaveInstalledVersion();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void AddToUserPath(string dir)
    {
        const string regKey = @"Environment";
        using var key = Registry.CurrentUser.OpenSubKey(regKey, writable: true);
        if (key is null) return;

        var current = key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";

        // Normalise separators and check if already present
        var parts = current.Split(';', StringSplitOptions.RemoveEmptyEntries);
        bool alreadyIn = parts.Any(p => string.Equals(p.Trim(), dir, StringComparison.OrdinalIgnoreCase));

        if (!alreadyIn)
        {
            var newPath = current.TrimEnd(';') + ";" + dir;
            key.SetValue("Path", newPath, RegistryValueKind.ExpandString);

            // Notify Windows that the environment changed
            SendPathChangedMessage();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint msg, UIntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    private static void SendPathChangedMessage()
    {
        try
        {
            UIntPtr result;
            SendMessageTimeout(
                new IntPtr(0xFFFF), // HWND_BROADCAST
                0x001A,             // WM_SETTINGCHANGE
                UIntPtr.Zero,
                "Environment",
                0x0002,             // SMTO_ABORTIFHUNG
                5000,
                out result);
        }
        catch { }
    }

    private static void ShowResult(bool success, string installDir)
    {
        DrawHeader();
        if (success)
        {
            DrawBox(new[]
            {
                $"  [bold #{C(Theme.Accent)}]Installation complete![/]",
                "",
                $"  [#{C(Theme.Muted)}]cash-ctrl.exe installed to:[/]",
                $"  [bold #{C(Theme.Secondary)}]{Markup.Escape(installDir)}[/]",
                "",
                $"  [#{C(Theme.Muted)}]The directory was added to your user PATH.[/]",
                $"  [#{C(Theme.Muted)}]Open a new terminal and run:[/]  [bold #{C(Theme.Primary)}]cash-ctrl --help[/]",
                "",
                $"  [#{C(Theme.Muted)}]Press any key to exit...[/]",
            });
        }
        else
        {
            DrawBox(new[]
            {
                $"  [bold red]Installation failed.[/]",
                "",
                $"  [#{C(Theme.Muted)}]Make sure you have write access to:[/]",
                $"  [bold #{C(Theme.Secondary)}]{Markup.Escape(installDir)}[/]",
                "",
                $"  [#{C(Theme.Muted)}]Press any key to exit...[/]",
            });
        }
    }

    // ── Rendering helpers ────────────────────────────────────────────────────

    private static void DrawHeader(string? subtitle = null, string? desc = null)
    {
        AnsiConsole.Clear();
        int w = Math.Max(Console.WindowWidth > 0 ? Console.WindowWidth : 80, 20);

        if (w >= FigletMinWidth)
        {
            AnsiConsole.Write(new FigletText(GetFont(), "CASH-CTRL").Centered().Color(Theme.Primary));
        }
        else if (w >= SmallFigletMin)
        {
            AnsiConsole.Write(new FigletText("CASH-CTRL").Centered().Color(Theme.Primary));
        }
        else
        {
            WriteCentered($"[bold #{C(Theme.Primary)}]CASH-CTRL[/]", w);
            Console.WriteLine();
        }

        WriteCentered($"[bold #{C(Theme.Primary)}]{subtitle ?? Subtitle}[/]", w);
        Console.WriteLine();
        WriteCentered($"[#{C(Theme.Muted)}]{Markup.Escape(desc ?? DescLine)}[/]", w);
        Console.WriteLine();
        AnsiConsole.Write(new Rule { Style = new Style(Theme.Border) });
        Console.WriteLine();
    }

    private static void DrawBox(IEnumerable<string> lines)
    {
        int w     = Math.Max(Console.WindowWidth > 0 ? Console.WindowWidth : 80, 20);
        int bw    = Math.Min(72, w - 4);
        int mx    = (w - bw) / 2;
        var brd   = $"#{110:X2}{100:X2}{160:X2}";
        var pad   = new string(' ', mx);
        int inner = bw - 2;

        AnsiConsole.Markup($"{pad}[{brd}]╭{new string('─', inner)}╮[/]");
        Console.WriteLine();

        foreach (var rawLine in lines)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(rawLine, @"\[.*?\]", "");
            string line = rawLine;
            // Truncate plain text if it overflows the box width
            if (plain.Length > inner)
            {
                int indent   = plain.Length - plain.TrimStart().Length;
                var trimmed  = plain.TrimStart();
                int maxText  = Math.Max(1, inner - indent - 1);
                if (trimmed.Length > maxText) trimmed = trimmed[..maxText] + "\u2026";
                line  = new string(' ', indent) + Markup.Escape(trimmed);
                plain = new string(' ', indent) + trimmed;
            }
            var fill = Math.Max(0, inner - plain.Length);
            AnsiConsole.Markup($"{pad}[{brd}]│[/]{line}{new string(' ', fill)}[{brd}]│[/]");
            Console.WriteLine();
        }

        AnsiConsole.Markup($"{pad}[{brd}]╰{new string('─', inner)}╯[/]");
        Console.WriteLine();
    }

    private static void WriteCentered(string markup, int width)
    {
        var plain = System.Text.RegularExpressions.Regex.Replace(markup, @"\[.*?\]", "");
        int pad   = Math.Max(0, (width - plain.Length) / 2);
        AnsiConsole.Markup(new string(' ', pad) + markup);
        Console.WriteLine();
    }

    private static string C(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";

    // ANSI Shadow font — shared with WelcomeScreen via assembly resources
    private static FigletFont? _figletFont;
    private static FigletFont GetFont()
    {
        if (_figletFont is not null) return _figletFont;
        var asm     = typeof(WelcomeScreen).Assembly;
        var resName = asm.GetManifestResourceNames()
                        .First(n => n.EndsWith("ansi-shadow.flf", StringComparison.OrdinalIgnoreCase));
        using var s = asm.GetManifestResourceStream(resName)!;
        _figletFont = FigletFont.Load(s);
        return _figletFont;
    }
}

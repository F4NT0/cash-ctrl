using System.Text.Json;

namespace CashCtrl.Services;

public static class VersionService
{
    private const string GitHubApiUrl =
        "https://api.github.com/repos/F4NT0/Cash-Ctrl/releases/latest";

    private const string AppDataDir =
        // Evaluated at runtime via property below
        "";

    private static string VersionFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CashCtrl", "version.json");

    // ── In-process cache ─────────────────────────────────────────────────────

    private static string? _latestVersion;
    private static bool    _checkDone;
    private static Task?   _checkTask;

    // ── Public API ───────────────────────────────────────────────────────────

    public static string Current => AppVersion.Current;

    /// <summary>
    /// Kicks off the remote version check in the background (fire-and-forget).
    /// Call once at startup; result will be ready by the time the UI is shown.
    /// </summary>
    public static void StartBackgroundCheck()
    {
        if (_checkTask is not null) return;
        _checkTask = Task.Run(async () =>
        {
            _latestVersion = await FetchLatestAsync();
            _checkDone     = true;
        });
    }

    /// <summary>Returns the latest remote version tag, or null if not fetched yet / failed.</summary>
    public static string? LatestVersion => _checkDone ? _latestVersion : null;

    /// <summary>True when the remote version is newer than the running binary.</summary>
    public static bool IsOutdated =>
        _checkDone && _latestVersion is not null && IsNewerThan(_latestVersion, Current);

    /// <summary>Returns the direct download URL of the exe asset in the latest release, or null.</summary>
    public static string? LatestExeDownloadUrl { get; private set; }

    // ── Installed-version persistence ────────────────────────────────────────

    /// <summary>Persists the current binary version to %APPDATA%\CashCtrl\version.json.</summary>
    public static void SaveInstalledVersion()
    {
        try
        {
            var dir = Path.GetDirectoryName(VersionFilePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new { installed = AppVersion.Current });
            File.WriteAllText(VersionFilePath, json);
        }
        catch { }
    }

    // ── GitHub fetch ─────────────────────────────────────────────────────────

    private static async Task<string?> FetchLatestAsync()
    {
        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.Timeout = TimeSpan.FromSeconds(2);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("cash-ctrl/" + AppVersion.Current);

            var json = await http.GetStringAsync(GitHubApiUrl);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            var tag  = root.GetProperty("tag_name").GetString();

            // Also grab exe asset URL if present
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        LatestExeDownloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            return tag;
        }
        catch
        {
            return null;
        }
    }

    // ── Version comparison ───────────────────────────────────────────────────

    /// <summary>Returns true if <paramref name="remote"/> is strictly newer than <paramref name="local"/>.</summary>
    private static bool IsNewerThan(string remote, string local)
    {
        static Version Parse(string v)
        {
            var s = v.TrimStart('v', 'V').Split('-')[0]; // strip pre-release suffix
            return System.Version.TryParse(s, out var ver) ? ver : new Version(0, 0, 0);
        }
        return Parse(remote) > Parse(local);
    }

    // ── --update flow ────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads the latest exe from GitHub, saves it to a temp file, runs it with --install,
    /// then prints instructions to restart the terminal.
    /// </summary>
    public static async Task PerformUpdateAsync()
    {
        var p    = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var acc  = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        var dim  = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var warn = "#FFD700";
        var red  = "#FF6B6B";

        Spectre.Console.AnsiConsole.Clear();
        Spectre.Console.AnsiConsole.MarkupLine($"[bold {p}]Cash-Ctrl Update[/]");
        Console.WriteLine();

        // Ensure we have the latest info
        if (_latestVersion is null)
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[{dim}]Checking for latest release...[/]");
            _latestVersion = await FetchLatestAsync();
            _checkDone = true;
        }

        if (_latestVersion is null)
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[{red}]Could not reach GitHub. Check your connection and try again.[/]");
            Console.ReadKey(true);
            return;
        }

        if (!IsOutdated)
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[{acc}]You are already on the latest version ({AppVersion.Current}).[/]");
            Console.ReadKey(true);
            return;
        }

        Spectre.Console.AnsiConsole.MarkupLine(
            $"[{dim}]Current:[/] [{p}]{AppVersion.Current}[/]   " +
            $"[{dim}]Latest:[/]  [{acc}]{_latestVersion}[/]");
        Console.WriteLine();

        var downloadUrl = LatestExeDownloadUrl;
        if (downloadUrl is null)
        {
            Spectre.Console.AnsiConsole.MarkupLine(
                $"[{warn}]No .exe asset found in the release. " +
                $"Download manually from:[/] [bold {p}]https://github.com/F4NT0/Cash-Ctrl/releases/latest[/]");
            Console.ReadKey(true);
            return;
        }

        // Download to temp file
        var tmpPath = Path.Combine(Path.GetTempPath(), "cash-ctrl-update.exe");
        Spectre.Console.AnsiConsole.MarkupLine($"[{dim}]Downloading {_latestVersion}...[/]");

        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.Timeout = TimeSpan.FromSeconds(60);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("cash-ctrl/" + AppVersion.Current);

            var bytes = await http.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(tmpPath, bytes);
        }
        catch (Exception ex)
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[{red}]Download failed: {Spectre.Console.Markup.Escape(ex.Message)}[/]");
            Console.ReadKey(true);
            return;
        }

        Spectre.Console.AnsiConsole.MarkupLine($"[{acc}]Download complete.[/]");
        Console.WriteLine();
        Spectre.Console.AnsiConsole.MarkupLine($"[{dim}]Installing update...[/]");

        // Resolve where the currently installed exe lives
        var currentExe = Environment.ProcessPath
                         ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                         ?? string.Empty;

        var defaultInstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CashCtrl");
        var installDir = string.IsNullOrEmpty(currentExe)
            ? defaultInstallDir
            : Path.GetDirectoryName(currentExe) ?? defaultInstallDir;
        var destExe = Path.Combine(installDir, "cash-ctrl.exe");

        try
        {
            Directory.CreateDirectory(installDir);

            // Can't overwrite the running exe directly on Windows — schedule via bat
            var bat = Path.Combine(Path.GetTempPath(), "cashctrl_update.bat");
            await File.WriteAllTextAsync(bat,
                $"@echo off\r\n" +
                $"timeout /t 2 /nobreak >nul\r\n" +
                $"copy /y \"{tmpPath}\" \"{destExe}\"\r\n" +
                $"del \"%~f0\"");

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = "cmd.exe",
                Arguments       = $"/c \"{bat}\"",
                WindowStyle     = System.Diagnostics.ProcessWindowStyle.Hidden,
                CreateNoWindow  = true,
                UseShellExecute = true,
            });

            // Ensure install dir is on PATH
            AddToUserPath(installDir);
            SaveInstalledVersion();
        }
        catch (Exception ex)
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[{red}]Install failed: {Spectre.Console.Markup.Escape(ex.Message)}[/]");
            Console.ReadKey(true);
            return;
        }

        Console.WriteLine();
        Spectre.Console.AnsiConsole.MarkupLine($"[bold {acc}] \u2714 cash-ctrl {_latestVersion} installed successfully.[/]");
        Spectre.Console.AnsiConsole.MarkupLine($"[{dim}]   The new executable will be active after this process exits.[/]");
        Spectre.Console.AnsiConsole.MarkupLine($"[{dim}]   Open a new terminal and run: [/][bold {p}]cash-ctrl[/]");
        Console.WriteLine();
        Spectre.Console.AnsiConsole.MarkupLine($"[{dim}]Press any key to exit...[/]");
        Console.ReadKey(true);
    }

    private static void AddToUserPath(string dir)
    {
        try
        {
            const string regKey = @"Environment";
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regKey, writable: true);
            if (key is null) return;

            var current = key.GetValue("Path", "", Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
            var parts   = current.Split(';', StringSplitOptions.RemoveEmptyEntries);
            bool alreadyIn = parts.Any(p => string.Equals(p.Trim(), dir, StringComparison.OrdinalIgnoreCase));
            if (!alreadyIn)
            {
                var newPath = current.TrimEnd(';') + ";" + dir;
                key.SetValue("Path", newPath, Microsoft.Win32.RegistryValueKind.ExpandString);
            }
        }
        catch { }
    }
}

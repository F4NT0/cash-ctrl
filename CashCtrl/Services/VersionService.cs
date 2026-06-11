using System.Text.Json;
using Spectre.Console;

namespace CashCtrl.Services;

public static class VersionService
{
    private const string GitHubApiUrl =
        "https://api.github.com/repos/F4NT0/Cash-Ctrl/releases/latest";

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
            using var http = BuildApiClient(timeout: TimeSpan.FromSeconds(10));

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

    // ── HttpClient factory ───────────────────────────────────────────────────

    /// <summary>
    /// Creates an HttpClient for the GitHub REST API (JSON responses).
    /// </summary>
    private static System.Net.Http.HttpClient BuildApiClient(TimeSpan timeout)
    {
        var http = new System.Net.Http.HttpClient();
        http.Timeout = timeout;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("cash-ctrl/" + AppVersion.Current);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    /// <summary>
    /// Creates an HttpClient for binary asset downloads (follows redirects).
    /// </summary>
    private static System.Net.Http.HttpClient BuildDownloadClient(TimeSpan timeout)
    {
        var handler = new System.Net.Http.HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
        };
        var http = new System.Net.Http.HttpClient(handler);
        http.Timeout = timeout;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("cash-ctrl/" + AppVersion.Current);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/octet-stream");
        return http;
    }

    // ── --update flow ────────────────────────────────────────────────────────

    /// <summary>
    /// Downloads the latest exe from GitHub releases and installs it, showing
    /// a step-by-step Spectre.Console Progress display for each operation.
    /// </summary>
    public static async Task PerformUpdateAsync()
    {
        var colP   = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var colAcc = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        var colDim = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";

        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("Cash-Ctrl")
                .Centered()
                .Color(Color.Purple));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold {colP}]  Update Manager[/]");
        AnsiConsole.WriteLine();

        // ── Pre-flight: ensure we know the latest release ────────────────────
        if (!_checkDone)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse($"bold {colAcc}"))
                .StartAsync($"[{colDim}]Contacting GitHub to check for updates...[/]", async _ =>
                {
                    _latestVersion = await FetchLatestAsync();
                    _checkDone = true;
                });
        }

        if (_latestVersion is null)
        {
            AnsiConsole.MarkupLine($"[red] Could not reach GitHub. Check your connection and try again.[/]");
            AnsiConsole.MarkupLine($"[{colDim}]Press any key to exit...[/]");
            Console.ReadKey(true);
            return;
        }

        if (!IsOutdated)
        {
            AnsiConsole.Write(new Panel(
                new Markup($"[bold {colAcc}] You are already on the latest version![/]\n\n" +
                           $"[{colDim}]Installed version:[/] [bold white]{AppVersion.Current}[/]"))
            {
                Header = new PanelHeader("[bold]No update available[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(2, 1, 2, 1),
            });
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[{colDim}]Press any key to exit...[/]");
            Console.ReadKey(true);
            return;
        }

        var downloadUrl = LatestExeDownloadUrl;
        if (downloadUrl is null)
        {
            AnsiConsole.MarkupLine(
                $"[yellow] No .exe asset found in the release. " +
                $"Download manually at:[/] [bold {colP}]https://github.com/F4NT0/Cash-Ctrl/releases/latest[/]");
            AnsiConsole.MarkupLine($"[{colDim}]Press any key to exit...[/]");
            Console.ReadKey(true);
            return;
        }

        // Show version summary before starting
        AnsiConsole.Write(new Rule($"[bold {colP}]Update available[/]").RuleStyle(Style.Parse(colDim)));
        AnsiConsole.MarkupLine($"  [{colDim}]Current version:[/]  [bold white]{AppVersion.Current}[/]");
        AnsiConsole.MarkupLine($"  [{colDim}]New version    :[/]  [bold {colAcc}]{_latestVersion}[/]");
        AnsiConsole.MarkupLine($"  [{colDim}]Source         :[/]  [{colDim}]{downloadUrl}[/]");
        AnsiConsole.WriteLine();

        // ── Resolve install directory ────────────────────────────────────────
        var currentExe = Environment.ProcessPath
                         ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                         ?? string.Empty;

        var defaultInstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CashCtrl");
        var installDir = string.IsNullOrEmpty(currentExe)
            ? defaultInstallDir
            : Path.GetDirectoryName(currentExe) ?? defaultInstallDir;
        var destExe  = Path.Combine(installDir, "cash-ctrl.exe");
        var tmpPath  = Path.Combine(Path.GetTempPath(), "cash-ctrl-update.exe");
        var batPath  = Path.Combine(Path.GetTempPath(), "cashctrl_update.bat");

        // ── Step-by-step Progress Display ────────────────────────────────────
        string? stepError = null;

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new SpinnerColumn(),
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn())
            .StartAsync(async ctx =>
            {
                // ── Step 1: Verify release on GitHub ────────────────────────
                var t1 = ctx.AddTask("[yellow]Step 1/5[/] Verifying release on GitHub", maxValue: 100);
                t1.Increment(50);
                await Task.Delay(300);
                t1.Description = $"[green]Step 1/5[/] Release [bold]{_latestVersion}[/] found on GitHub \u2714";
                t1.Increment(50);
                await Task.Delay(200);

                // ── Step 2: Download executable ──────────────────────────────
                var t2 = ctx.AddTask("[yellow]Step 2/5[/] Downloading cash-ctrl.exe from GitHub...", maxValue: 100);
                t2.Increment(5);

                try
                {
                    // Use streaming download with progress reporting
                    using var http = BuildDownloadClient(timeout: TimeSpan.FromMinutes(5));

                    // HEAD to get content length (best-effort)
                    long totalBytes = 0;
                    try
                    {
                        using var head = await http.SendAsync(
                            new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, downloadUrl));
                        totalBytes = head.Content.Headers.ContentLength ?? 0;
                    }
                    catch { }

                    using var response = await http.GetAsync(
                        downloadUrl,
                        System.Net.Http.HttpCompletionOption.ResponseHeadersRead);

                    response.EnsureSuccessStatusCode();

                    if (totalBytes == 0)
                        totalBytes = response.Content.Headers.ContentLength ?? 0;

                    await using var contentStream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream    = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                    var buffer    = new byte[81920];
                    long received = 0;
                    int  read;

                    while ((read = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read));
                        received += read;

                        if (totalBytes > 0)
                        {
                            // map received bytes to 5–95 % of this task
                            var pct = (double)received / totalBytes * 90.0;
                            t2.Value = 5 + pct;
                        }
                        else
                        {
                            // unknown size — pulse spinner near 50 %
                            if (t2.Value < 80) t2.Increment(0.5);
                        }
                    }

                    var sizeMb = received / 1_048_576.0;
                    t2.Value = 100;
                    t2.Description = $"[green]Step 2/5[/] Download complete \u2714  ({sizeMb:F1} MB saved to temp)";
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    t2.Value = 100;
                    t2.Description = $"[red]Step 2/5[/] Download failed \u2718  ({Markup.Escape(ex.Message)})";
                    stepError = ex.Message;
                    return;
                }

                // ── Step 3: Prepare install directory ───────────────────────
                var t3 = ctx.AddTask("[yellow]Step 3/5[/] Preparing install directory...", maxValue: 100);
                t3.Increment(30);
                await Task.Delay(200);

                try
                {
                    Directory.CreateDirectory(installDir);
                    t3.Description = $"[green]Step 3/5[/] Directory ready \u2714  ({installDir})";
                    t3.Value = 100;
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    t3.Value = 100;
                    t3.Description = $"[red]Step 3/5[/] Failed to create directory \u2718  ({Markup.Escape(ex.Message)})";
                    stepError = ex.Message;
                    return;
                }

                // ── Step 4: Create replacement script (.bat) ────────────────
                var t4 = ctx.AddTask("[yellow]Step 4/5[/] Creating executable replacement script...", maxValue: 100);
                t4.Increment(20);
                await Task.Delay(200);

                try
                {
                    // The bat retries the copy up to 10 times (1 s apart) to handle
                    // the case where the parent process hasn't fully exited yet,
                    // then verifies that the destination file exists before cleaning up.
                    var batContent =
                        "@echo off\r\n" +
                        "setlocal\r\n" +
                        "set RETRIES=0\r\n" +
                        ":retry\r\n" +
                        "timeout /t 1 /nobreak >nul\r\n" +
                        $"copy /y \"{tmpPath}\" \"{destExe}\" >nul 2>&1\r\n" +
                        "if errorlevel 1 (\r\n" +
                        "  set /a RETRIES+=1\r\n" +
                        "  if %RETRIES% lss 10 goto retry\r\n" +
                        ")\r\n" +
                        $"if exist \"{destExe}\" (\r\n" +
                        $"  del \"{tmpPath}\" >nul 2>&1\r\n" +
                        ")\r\n" +
                        "del \"%~f0\"\r\n";

                    await File.WriteAllTextAsync(batPath, batContent);

                    t4.Description = $"[green]Step 4/5[/] Replacement script created \u2714  ({batPath})";
                    t4.Value = 100;
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    t4.Value = 100;
                    t4.Description = $"[red]Step 4/5[/] Failed to create script \u2718  ({Markup.Escape(ex.Message)})";
                    stepError = ex.Message;
                    return;
                }

                // ── Step 5: Start installation and update PATH ───────────────
                var t5 = ctx.AddTask("[yellow]Step 5/5[/] Starting installation and updating PATH...", maxValue: 100);
                t5.Increment(20);
                await Task.Delay(200);

                try
                {
                    // Launch the bat detached so it outlives this process
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName        = "cmd.exe",
                        Arguments       = $"/c \"{batPath}\"",
                        WindowStyle     = System.Diagnostics.ProcessWindowStyle.Hidden,
                        CreateNoWindow  = true,
                        UseShellExecute = true,
                    });

                    t5.Increment(40);
                    await Task.Delay(300);

                    AddToUserPath(installDir);
                    SaveInstalledVersion();

                    t5.Description = $"[green]Step 5/5[/] Installation scheduled and PATH updated \u2714";
                    t5.Value = 100;
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    t5.Value = 100;
                    t5.Description = $"[red]Step 5/5[/] Installation failed \u2718  ({Markup.Escape(ex.Message)})";
                    stepError = ex.Message;
                }
            });

        // ── Final summary ────────────────────────────────────────────────────
        AnsiConsole.WriteLine();

        if (stepError is not null)
        {
            AnsiConsole.Write(new Panel(
                new Markup($"[red bold] Update failed[/]\n\n" +
                           $"[white]Error: {Markup.Escape(stepError)}[/]\n\n" +
                           $"[{colDim}]You can download manually at:\n" +
                           $"[/][bold {colP}]https://github.com/F4NT0/Cash-Ctrl/releases/latest[/]"))
            {
                Header  = new PanelHeader("[bold red]Error[/]"),
                Border  = BoxBorder.Rounded,
                Padding = new Padding(2, 1, 2, 1),
            });
        }
        else
        {
            AnsiConsole.Write(new Panel(
                new Markup($"[bold {colAcc}] cash-ctrl {_latestVersion} installed successfully![/]\n\n" +
                           $"[{colDim}]Executable installed at:[/] [bold white]{destExe}[/]\n" +
                           $"[{colDim}]The new executable will be active after closing this process.[/]\n\n" +
                           $"[{colDim}]Open a new terminal and run:[/] [bold {colP}]cash-ctrl[/]"))
            {
                Header  = new PanelHeader("[bold]Update Complete[/]"),
                Border  = BoxBorder.Rounded,
                Padding = new Padding(2, 1, 2, 1),
            });
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[{colDim}]Press any key to exit...[/]");
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

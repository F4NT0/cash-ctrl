#:package Spectre.Console@0.49.1

using Spectre.Console;
using System.Diagnostics;
using System.Text.RegularExpressions;

// BuildRelease.cs - Script de build e release automatizado para Cash-Ctrl
// Uso: dotnet run BuildRelease.cs  (ou: dotnet run --file BuildRelease.cs)

AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
{
    Ansi = AnsiSupport.Detect,
    ColorSystem = ColorSystemSupport.Detect,
    Interactive = InteractionSupport.Yes,
    Out = new AnsiConsoleOutput(Console.Out),
});

var version = AnsiConsole.Ask<string>("What is the release version? (ex: 1.0.2 ou 1.0.2-beta)", "1.0.1");

// Validate version format
if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+(-\w+)?$"))
{
    AnsiConsole.MarkupLine("[red]Invalid version! Use the format X.Y.Z or X.Y.Z-beta (ex: 1.0.2 or 1.0.2-beta)[/]");
    return 1;
}

var versionTag = $"v{version}";
var projectFile = "CashCtrl/CashCtrl.csproj";
var appVersionFile = "CashCtrl/AppVersion.cs";
var publishDir = "dist";
var exeName = "cash-ctrl.exe";
var exePath = Path.Combine(publishDir, exeName);

AnsiConsole.Clear();
AnsiConsole.Write(
    new FigletText("Cash-Ctrl")
        .Centered()
        .Color(Color.Purple));

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[bold cyan]Build & Release Script[/]");
AnsiConsole.WriteLine();

// Track any error that occurred inside the progress context
int exitCode = 0;

await AnsiConsole.Progress()
    .AutoRefresh(true)
    .AutoClear(false)
    .HideCompleted(false)
    .Columns(
        new SpinnerColumn(),
        new TaskDescriptionColumn(),
        new ProgressBarColumn(),
        new PercentageColumn(),
        new ElapsedTimeColumn())
    .StartAsync(async ctx =>
    {
        // ── Step 1: Update AppVersion.cs ─────────────────────────────────
        var task1 = ctx.AddTask("[yellow]Step 1[/] — Updating AppVersion.cs", maxValue: 100);
        task1.Description = "[yellow]Step 1[/] — Reading AppVersion.cs...";
        task1.Increment(20);
        await Task.Delay(1000);

        try
        {
            var appVersionContent = await File.ReadAllTextAsync(appVersionFile);

            task1.Description = $"[yellow]Step 1[/] — Replacing version tag with {versionTag}...";
            task1.Increment(40);
            await Task.Delay(1000);

            appVersionContent = Regex.Replace(appVersionContent, @"public const string Current = "".*""",
                $"public const string Current = \"{versionTag}\"");
            await File.WriteAllTextAsync(appVersionFile, appVersionContent);

            task1.Description = $"[green]Step 1[/] — AppVersion.cs updated \u2714 (version set to {versionTag})";
            task1.Increment(40);
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            task1.Description = $"[red]Step 1[/] — Failed to update AppVersion.cs \u2718 ({ex.Message})";
            task1.Value = task1.MaxValue;
            exitCode = 1;
            return;
        }

        // ── Step 2: Update CashCtrl.csproj ───────────────────────────────
        var task2 = ctx.AddTask("[yellow]Step 2[/] — Updating CashCtrl.csproj", maxValue: 100);
        task2.Description = "[yellow]Step 2[/] — Reading CashCtrl.csproj...";
        task2.Increment(20);
        await Task.Delay(1000);

        try
        {
            var csprojContent = await File.ReadAllTextAsync(projectFile);

            task2.Description = $"[yellow]Step 2[/] — Setting <Version> to {version}...";
            task2.Increment(40);
            await Task.Delay(1000);

            csprojContent = Regex.Replace(csprojContent, @"<Version>.*</Version>",
                $"<Version>{version}</Version>");
            await File.WriteAllTextAsync(projectFile, csprojContent);

            task2.Description = $"[green]Step 2[/] — CashCtrl.csproj updated \u2714 (version set to {version})";
            task2.Increment(40);
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            task2.Description = $"[red]Step 2[/] — Failed to update CashCtrl.csproj \u2718 ({ex.Message})";
            task2.Value = task2.MaxValue;
            exitCode = 1;
            return;
        }

        // ── Step 3: dotnet publish ────────────────────────────────────────
        var task3 = ctx.AddTask("[yellow]Step 3[/] — Building & publishing executable", maxValue: 100);
        task3.Description = "[yellow]Step 3[/] — Cleaning previous dist folder...";
        task3.Increment(10);
        await Task.Delay(1000);

        try
        {
            if (Directory.Exists(publishDir))
                Directory.Delete(publishDir, true);

            task3.Description = "[yellow]Step 3[/] — Running dotnet publish (win-x64, self-contained)...";
            task3.Increment(20);
            await Task.Delay(1000);

            var publishResult = await RunCommandAsync("dotnet",
                $"publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o {publishDir}");

            task3.Increment(50);
            await Task.Delay(1000);

            if (publishResult.ExitCode != 0)
            {
                task3.Description = "[red]Step 3[/] — Publish failed \u2718 (dotnet publish returned a non-zero exit code)";
                task3.Value = task3.MaxValue;
                exitCode = 1;
                return;
            }

            if (!File.Exists(exePath))
            {
                task3.Description = $"[red]Step 3[/] — Publish failed \u2718 (executable not found at {exePath})";
                task3.Value = task3.MaxValue;
                exitCode = 1;
                return;
            }

            task3.Description = $"[green]Step 3[/] — Executable created \u2714 ({exePath})";
            task3.Increment(20);
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            task3.Description = $"[red]Step 3[/] — Build error \u2718 ({ex.Message})";
            task3.Value = task3.MaxValue;
            exitCode = 1;
        }
    });

if (exitCode != 0)
    return exitCode;

// Final summary
AnsiConsole.WriteLine();
AnsiConsole.Write(
    new Panel(
        new Markup($"[bold green]Build & Release Complete![/]\n\n" +
                  $"[white]Version : {versionTag}[/]\n" +
                  $"[white]Executable : {exePath}[/]\n" +
                  $"[cyan]Release URL: https://github.com/F4NT0/Cash-Ctrl/releases/tag/{versionTag}[/]"))
    {
        Header = new PanelHeader("[bold cyan]Summary[/]"),
        Border = BoxBorder.Rounded,
        Padding = new Padding(2, 1, 2, 1)
    });

return 0;

// ── Helper ────────────────────────────────────────────────────────────────────
async Task<(int ExitCode, string Output)> RunCommandAsync(string command, string args)
{
    var processInfo = new ProcessStartInfo
    {
        FileName = command,
        Arguments = args,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(processInfo);
    if (process == null)
        return (1, "Failed to start process");

    var output = await process.StandardOutput.ReadToEndAsync();
    var error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    return (process.ExitCode, output + error);
}


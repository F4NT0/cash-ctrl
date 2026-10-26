using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using Spectre.Console;

const string Repo = "F4NT0/Cash-Ctrl";
const string ExeName = "cash-ctrl.exe";
const string ProcessName = "cash-ctrl";

var installDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CashCtrl");
var destExe = Path.Combine(installDir, ExeName);
var dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CashCtrl");

Console.OutputEncoding = System.Text.Encoding.UTF8;
AnsiConsole.Clear();
AnsiConsole.Write(new FigletText("Cash-Ctrl").Centered().Color(Color.Purple));
AnsiConsole.Write(new Rule("[bold]Installer[/]").RuleStyle("grey"));
AnsiConsole.WriteLine();

var plan = new Table().Border(TableBorder.Rounded).AddColumn("#").AddColumn("Step");
string[] steps =
[
    "Check for an existing installation",
    "Fetch the latest release from GitHub",
    "Download " + ExeName,
    "Install to " + installDir,
    "Add install directory to the user PATH",
    "Verify the installation",
];
for (var i = 0; i < steps.Length; i++) plan.AddRow((i + 1).ToString(), Markup.Escape(steps[i]));
AnsiConsole.Write(new Panel(plan) { Header = new PanelHeader("Installation plan"), Border = BoxBorder.Rounded });
AnsiConsole.WriteLine();

if (!AnsiConsole.Confirm("Proceed with the installation?")) return 0;
AnsiConsole.WriteLine();

var tmpPath = Path.Combine(Path.GetTempPath(), "cash-ctrl-install.exe");
string? tag = null, url = null;
var failed = false;

void Ok(string m) => AnsiConsole.MarkupLine($"[green]\u2714[/] {m}");
void Warn(string m) => AnsiConsole.MarkupLine($"[yellow]![/] {m}");
void Fail(string m) { AnsiConsole.MarkupLine($"[red]\u2718[/] {m}"); failed = true; }
void Step(int n) => AnsiConsole.MarkupLine($"[bold purple]Step {n}/{steps.Length}[/] {Markup.Escape(steps[n - 1])}");

try
{
    // Step 1
    Step(1);
    var existing = File.Exists(destExe);
    if (existing)
    {
        Warn($"Existing installation found at [bold]{Markup.Escape(destExe)}[/]");
        if (!AnsiConsole.Confirm("Delete the old version and install the new one?"))
        {
            AnsiConsole.MarkupLine("[grey]Installation cancelled.[/]");
            return 0;
        }
        foreach (var p in Process.GetProcessesByName(ProcessName))
        {
            try { p.Kill(); p.WaitForExit(5000); Ok($"Stopped running process {p.Id}"); }
            catch (Exception ex) { Warn($"Could not stop process {p.Id}: {Markup.Escape(ex.Message)}"); }
        }
        File.Delete(destExe);
        Ok("Old version removed");
    }
    else Ok("No previous installation found");

    // Step 2
    Step(2);
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("cash-ctrl-installer");
    http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Contacting GitHub...", async _ =>
    {
        var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
        using var doc = JsonDocument.Parse(json);
        tag = doc.RootElement.GetProperty("tag_name").GetString();
        foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if ((a.GetProperty("name").GetString() ?? "").EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                url = a.GetProperty("browser_download_url").GetString();
                break;
            }
        }
    });
    if (url is null) { Fail("No .exe asset found in the latest release"); return 1; }
    Ok($"Latest release: [bold]{Markup.Escape(tag ?? "?")}[/]");

    // Step 3
    Step(3);
    await AnsiConsole.Progress()
        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(),
                 new DownloadedColumn(), new TransferSpeedColumn())
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("Downloading", maxValue: 1);
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength;
            if (total is > 0) task.MaxValue = total.Value; else task.IsIndeterminate = true;
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buf = new byte[81920];
            int n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n));
                task.Increment(n);
            }
            task.Value = task.MaxValue;
        });
    Ok($"Downloaded {new FileInfo(tmpPath).Length / 1_048_576.0:F1} MB");

    // Step 4
    Step(4);
    Directory.CreateDirectory(installDir);
    File.Copy(tmpPath, destExe, overwrite: true);
    File.Delete(tmpPath);
    Directory.CreateDirectory(dataDir);
    File.WriteAllText(Path.Combine(dataDir, "version.json"),
        JsonSerializer.Serialize(new { installed = (tag ?? "").TrimStart('v', 'V') }));
    Ok($"Installed to [bold]{Markup.Escape(destExe)}[/]");

    // Step 5
    Step(5);
    using (var key = Registry.CurrentUser.OpenSubKey("Environment", writable: true))
    {
        var current = key?.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
        if (key is null) Warn("Could not open the user environment registry key");
        else if (current.Split(';', StringSplitOptions.RemoveEmptyEntries)
                 .Any(p => string.Equals(p.Trim(), installDir, StringComparison.OrdinalIgnoreCase)))
            Ok("Already in PATH");
        else
        {
            key.SetValue("Path", current.TrimEnd(';') + ";" + installDir, RegistryValueKind.ExpandString);
            Ok("Added to user PATH (open a new terminal to use it)");
        }
    }

    // Step 6
    Step(6);
    if (File.Exists(destExe) && new FileInfo(destExe).Length > 0) Ok("Executable is in place");
    else Fail("Executable not found after installation");
}
catch (Exception ex)
{
    Fail(Markup.Escape(ex.Message));
}

AnsiConsole.WriteLine();
AnsiConsole.Write(new Panel(failed
    ? new Markup($"[red bold]Installation failed[/]\n\nManual download: https://github.com/{Repo}/releases/latest")
    : new Markup($"[green bold]Cash-Ctrl {Markup.Escape(tag ?? "")} installed successfully![/]\n\nOpen a new terminal and run: [bold purple]cash-ctrl[/]"))
{
    Border = BoxBorder.Rounded,
    Padding = new Padding(2, 1, 2, 1),
});
AnsiConsole.MarkupLine("[grey]Press any key to exit...[/]");
Console.ReadKey(true);
return failed ? 1 : 0;

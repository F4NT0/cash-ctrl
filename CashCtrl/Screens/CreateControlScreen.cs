using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class CreateControlScreen
{
    private static readonly System.Globalization.CultureInfo BrCulture =
        new("pt-BR");

    public static async Task ShowAsync(string? defaultName = null, string? workingDirectory = null)
    {
        var cwd = workingDirectory ?? Directory.GetCurrentDirectory();

        // ── Step 1: Get control name ─────────────────────────────────────────
        AnsiConsole.Clear();
        RenderForm(null, null, null, null);

        string controlName;
        while (true)
        {
            AnsiConsole.Markup($"  {Theme.Purple("Control name")} › ");
            controlName = (defaultName is null ? Console.ReadLine() : defaultName) ?? string.Empty;
            defaultName = null; // only use default once

            controlName = controlName.Trim();
            if (!string.IsNullOrEmpty(controlName)) break;

            AnsiConsole.MarkupLine($"  {Theme.Gray("(name cannot be empty, try again)")}");
        }

        // Slugify: spaces → hyphens for the file name; keep display name as-is
        var fileSlug = controlName.Replace(' ', '-');
        var filePath = Path.Combine(cwd, fileSlug + ".json");

        // ── Step 2: Get total money ──────────────────────────────────────────
        AnsiConsole.Clear();
        RenderForm(controlName, fileSlug, filePath, null);

        decimal totalMoney = 0m;
        while (true)
        {
            AnsiConsole.Markup($"  {Theme.Purple("Total money")} (R$) › ");
            var raw = Console.ReadLine() ?? string.Empty;
            raw = raw.Trim().Replace("R$", "").Replace(" ", "").Replace(".", "").Replace(",", ".");

            if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
            {
                totalMoney = parsed;
                break;
            }

            AnsiConsole.MarkupLine($"  {Theme.Gray("(enter a valid amount, e.g. 25000 or 25000,00)")}");
        }

        // ── Step 3: Confirm ─────────────────────────────────────────────────
        AnsiConsole.Clear();
        RenderForm(controlName, fileSlug, filePath, totalMoney);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"  {Theme.Green("Enter")} {Theme.Gray("to create")}   {Theme.Gray("|")}   {Theme.Gray("Esc")} {Theme.Gray("to cancel")}");
        AnsiConsole.WriteLine();

        while (true)
        {
            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Theme.PrimaryStyle)
                    .StartAsync(Theme.Purple("Creating control…"), async _ =>
                    {
                        await ControlService.CreateControlAsync(filePath, controlName, totalMoney); // display name stored in favorites
                        await Task.Delay(600);
                    });

                var control = await ControlService.LoadControlAsync(filePath);
                if (control is not null)
                    MainScreen.Show(control);
                return;
            }

            if (key.Key == ConsoleKey.Escape) return;
        }
    }

    private static void RenderForm(string? name, string? fileSlug, string? filePath, decimal? totalMoney)
    {
        // ── colors ────────────────────────────────────────────────────────────
        // Labels: bright secondary; Values: white; path: cyan; money: green
        var labelColor = $"#{Theme.Secondary.R:X2}{Theme.Secondary.G:X2}{Theme.Secondary.B:X2}";
        var dimColor   = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";

        string Label(string t) => $"[{labelColor}]{Markup.Escape(t)}[/]";
        string Dim(string t)   => $"[{dimColor}]{Markup.Escape(t)}[/]";

        var nameDisplay  = name is null
            ? Dim("_")
            : $"[white]{Markup.Escape(name)}[/]";

        string pathDisplay;
        if (filePath is null)
        {
            pathDisplay = Dim("(enter name to see path)");
        }
        else
        {
            // Show dir in dim, filename slug in cyan
            var dir      = Path.GetDirectoryName(filePath) ?? string.Empty;
            var fileName = Path.GetFileName(filePath);
            pathDisplay  = $"{Dim(dir + Path.DirectorySeparatorChar)}[#{Theme.Info.R:X2}{Theme.Info.G:X2}{Theme.Info.B:X2}]{Markup.Escape(fileName)}[/]";
        }

        var moneyDisplay = totalMoney is null
            ? Dim("_")
            : $"[bold #{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}]{Markup.Escape(totalMoney.Value.ToString("C2", BrCulture))}[/]";

        // ── divider ───────────────────────────────────────────────────────────
        var divColor = $"#{Theme.Border.R:X2}{Theme.Border.G:X2}{Theme.Border.B:X2}";
        var div = $"[{divColor}]{'─'.ToString().PadRight(63, '─')}[/]";

        // ── hint line ─────────────────────────────────────────────────────────
        var hints = $"  [{labelColor}]Enter:[/] [white bold]create new control[/]   [{dimColor}]|[/]   [{labelColor}]Esc:[/] [white]exit[/]";

        var rows = string.Join("\n",
            $"  {Label("Control name:")}   {nameDisplay}",
            $"  {Label("Location:")}       {pathDisplay}",
            $"  {Label("Total money:")}    {moneyDisplay}",
            "",
            div,
            hints
        );

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Padder(
                Theme.MakePanelBright(new Markup(rows), " Creating new Control ")
            ).PadLeft(2).PadRight(2)
        );
        AnsiConsole.WriteLine();
    }
}

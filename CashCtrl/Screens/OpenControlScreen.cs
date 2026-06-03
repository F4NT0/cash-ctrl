using CashCtrl.Models;
using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class OpenControlScreen
{
    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static async Task ShowAsync(string filePath)
    {
        var control = await ControlService.LoadControlAsync(filePath);

        if (control is null)
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine($"[red]Could not open file:[/] {Markup.Escape(filePath)}");
            Console.ReadKey(true);
            return;
        }

        await ShowControlPreviewAsync(control);
    }

    public static async Task ShowControlPreviewAsync(ControlFile control)
    {
        while (true)
        {
            Draw(control, confirmDelete: false);

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                await ControlService.AddToFavoritesAsync(control.FilePath, control.Name);
                AnsiConsole.Clear();
                MainScreen.Show(control);
                return;
            }

            if (key.Key == ConsoleKey.Escape) { AnsiConsole.Clear(); return; }

            if (key.Key == ConsoleKey.Delete || key.KeyChar is 'd' or 'D')
            {
                // Ask for confirmation
                Draw(control, confirmDelete: true);
                var confirm = Console.ReadKey(true);
                if (confirm.Key == ConsoleKey.Enter)
                {
                    try { File.Delete(control.FilePath); } catch { }
                    await ControlService.RemoveFromFavoritesAsync(control.FilePath);
                    AnsiConsole.Clear();
                    return;
                }
                // any other key → back to normal view
            }
        }
    }

    private static void Draw(ControlFile control, bool confirmDelete)
    {
        int w     = Math.Max(Console.WindowWidth  > 0 ? Console.WindowWidth  : 80, 40);
        int h     = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 10);
        int mw    = Math.Min(70, w - 4);
        int inner = mw - 2;
        var brd   = "#6E64A0";
        var dim   = Hex(Theme.Muted);
        var sec   = Hex(Theme.Secondary);
        var foc   = Hex(Theme.Focus);
        var red   = "#FF6B6B";

        var period   = control.Periods.Values.FirstOrDefault();
        var total    = (period?.TotalValue ?? 0m) + (period?.TotalIncome ?? 0m);
        var currency = new System.Globalization.CultureInfo("pt-BR");

        string Row(string content)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(content, @"\[.*?\]", "");
            var pad   = Math.Max(0, inner - plain.Length);
            return $"[{brd}]│[/]{content}{new string(' ', pad)}[{brd}]│[/]";
        }

        string Sep(string l = "├", string r = "┤") =>
            $"[{brd}]{l}{new string('─', inner)}{r}[/]";

        // Truncate file path to fit
        var filePath = control.FilePath;
        var maxPath  = inner - 10;
        if (filePath.Length > maxPath && maxPath > 6)
            filePath = "…" + filePath[^(maxPath - 1)..];

        var titleName = control.Name.Length > inner - 4
            ? control.Name[..(inner - 4)] + "…"
            : control.Name;

        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', inner)}╮[/]",
            Row($"  [bold white]{Markup.Escape(titleName)}[/]"),
            Sep(),
            Row($"  [{dim}]Name :[/]  [{sec}]{Markup.Escape(control.Name)}[/]"),
            Row($"  [{dim}]Value:[/]  [bold {Hex(Theme.Accent)}]{Markup.Escape(total.ToString("C2", currency))}[/]"),
            Row($"  [{dim}]File :[/]  [{Hex(Theme.Info)}]{Markup.Escape(filePath)}[/]"),
            Sep(),
        };

        if (confirmDelete)
        {
            lines.Add(Row($"  [bold {red}]Delete \"{Markup.Escape(control.Name)}\"? This cannot be undone.[/]"));
            lines.Add(Sep("├", "┤"));
            lines.Add(Row($"  [bold {red}]Enter[/][{dim}] confirm delete   [/][bold {foc}]any other key[/][{dim}] cancel[/]"));
        }
        else
        {
            lines.Add(Row($"  [bold {foc}]Enter[/][{dim}] open   [/][bold {red}]Delete / D[/][{dim}] delete control   [/][bold {dim}]Esc[/][{dim}] back[/]"));
        }

        lines.Add($"[{brd}]╰{new string('─', inner)}╯[/]");

        int mh = lines.Count;
        int mx = Math.Max(0, (w - mw) / 2);
        int my = Math.Max(0, (h - mh) / 2);

        Console.CursorVisible = false;
        Console.SetCursorPosition(0, 0);
        // Fill screen to erase stale content
        for (int row = 0; row < h; row++)
        {
            try { Console.SetCursorPosition(0, row); } catch { }
            Console.Write(new string(' ', w));
        }

        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
            var plain = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "");
            Console.Write(new string(' ', Math.Max(0, mw - plain.Length)));
        }
    }
}

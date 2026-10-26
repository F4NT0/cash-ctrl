using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class HelpModal
{
    private static readonly (string Section, (string Key, string Desc)[] Items)[] Sections =
    {
        ("General", new[]
        {
            ("?", "Show this help"),
            ("Esc", "Go back / return to the start screen"),
            ("Q", "Quit the software"),
            ("K", "Open the calculator"),
            ("S", "Cycle the calendar month"),
        }),
        ("Panels", new[]
        {
            ("C", "Controls panel (Enter load, N new, D delete)"),
            ("B", "Balance panel (press again to edit balance)"),
            ("I", "Income panel (press again to add income)"),
            ("E", "Add a new expense"),
            ("L", "Entry list (Up/Down, Enter details)"),
        }),
        ("Entry list", new[]
        {
            ("U", "Edit selected entry"),
            ("D", "Delete mode (Space select, Enter confirm)"),
            ("F", "Filter (Tab column, Enter apply)"),
        }),
        ("Calculator", new[]
        {
            ("Enter", "Calculate"),
            ("Del", "Clear current line"),
            ("Esc", "Close (history is kept until you quit)"),
        }),
    };

    public static void Show()
    {
        int w = Math.Max(Console.WindowWidth > 0 ? Console.WindowWidth : 80, 40);
        int h = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 8);
        int mw = Math.Min(64, w - 4);
        int inner = mw - 2;
        var brd = "#6E64A0";
        var dim = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var acc = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";

        string Row(string markup, int plainLen) =>
            $"[{brd}]│[/]{markup}{new string(' ', Math.Max(0, inner - plainLen))}[{brd}]│[/]";

        var title = " Help ";
        var lines = new List<string>
        {
            $"[{brd}]╭─[/][bold {acc}]{title}[/][{brd}]{new string('─', inner - 1 - title.Length)}╮[/]",
        };

        foreach (var (section, items) in Sections)
        {
            lines.Add(Row($" [bold white]{Markup.Escape(section)}[/]", 1 + section.Length));
            foreach (var (key, desc) in items)
            {
                var d = desc.Length > inner - 12 ? desc[..(inner - 12)] : desc;
                lines.Add(Row($"   [bold {acc}]{Markup.Escape(key.PadRight(6))}[/][{dim}]{Markup.Escape(d)}[/]", 9 + d.Length));
            }
        }

        lines.Add(Row("", 0));
        lines.Add(Row($" [{dim}]Press any key to close...[/]", 29));
        lines.Add($"[{brd}]╰{new string('─', inner)}╯[/]");

        int mx = (w - mw) / 2;
        int my = Math.Max(0, (h - lines.Count) / 2);
        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
        }

        Console.ReadKey(true);
    }
}

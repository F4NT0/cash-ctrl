using CashCtrl.Models;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class ExpenseDetailModal
{
    private static readonly System.Globalization.CultureInfo Br = new("pt-BR");

    public static async Task ShowAsync(ControlEntry entry)
    {
        while (true)
        {
            DrawModal(entry);
            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Escape || k.Key == ConsoleKey.Enter)
                break;
        }
        await Task.CompletedTask;
    }

    private static void DrawModal(ControlEntry entry)
    {
        int w  = Math.Max(Console.WindowWidth,  60);
        int h  = Math.Max(Console.WindowHeight, 20);
        int mw = Math.Min(72, w - 4);
        int mh = 8 + Math.Max(1, entry.Details.Count) + 2;
        int mx = (w - mw) / 2;
        int my = Math.Max(0, (h - mh) / 2);

        var lines = BuildLines(entry, mw);

        Console.CursorVisible = false;
        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
            var plain = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "");
            Console.Write(new string(' ', Math.Max(0, mw - plain.Length)));
        }
    }

    private static List<string> BuildLines(ControlEntry entry, int mw)
    {
        var p   = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var sec = $"#{Theme.Secondary.R:X2}{Theme.Secondary.G:X2}{Theme.Secondary.B:X2}";
        var dim = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var acc = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        var red = "#FF6B6B";
        var brd = $"#{110:X2}{100:X2}{160:X2}";
        var inner = mw - 2;

        string Row(string content)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(content, @"\[.*?\]", "");
            var pad   = Math.Max(0, inner - plain.Length);
            return $"[{brd}]│[/]{content}{new string(' ', pad)}[{brd}]│[/]";
        }

        string Sep(string left = "├", string right = "┤") =>
            $"[{brd}]{left}{new string('─', inner)}{right}[/]";

        var totalStr  = entry.Total.ToString("C2", Br);
        var typeHex   = entry.TypeColor is { Length: > 0 } tc ? $"#{tc}" : dim;
        var entryName = entry.Description ?? entry.Type ?? "Expense";

        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', inner)}╮[/]",
            Row(""),
            Row($"  [{dim}]Name:[/]   [{sec}]{Markup.Escape(entryName)}[/]"),
            Row($"  [{dim}]Date:[/]   [{sec}]{Markup.Escape(entry.Date)}[/]"),
            Row($"  [{dim}]Type:[/]   [{typeHex}]{Markup.Escape(entry.Type ?? "-")}[/]"),
            Row($"  [{dim}]Total:[/]  [{red}]{Markup.Escape(totalStr)}[/]"),
            Row(""),
            Sep(),
            Row($"  [{dim}]{"Name",-24}{"Value",12}{"Qty",6}  {"Unit",-6}[/]"),
            Sep(),
        };

        if (entry.Details.Count == 0)
        {
            lines.Add(Row($"  [{dim}](no items)[/]"));
        }
        else
        {
            foreach (var item in entry.Details)
            {
                var amtStr = item.Amount.ToString("C2", Br);
                var qtyStr = item.Quantity.ToString();
                var nameStr = (item.Name.Length > 24 ? item.Name[..24] : item.Name).PadRight(24);
                var valStr  = amtStr.PadLeft(12);
                var qStr    = qtyStr.PadLeft(6);
                var uStr    = item.Size.PadRight(6);
                lines.Add(Row($"  [{sec}]{Markup.Escape(nameStr)}[/][{acc}]{Markup.Escape(valStr)}[/][{dim}]{Markup.Escape(qStr)}  {Markup.Escape(uStr)}[/]"));
            }
        }

        lines.Add(Sep("╰", "╯"));
        lines.Add(Row($"  [{dim}]Enter / Esc: close[/]"));
        lines.Add($"[{brd}]╰{new string('─', inner)}╯[/]");

        return lines;
    }
}

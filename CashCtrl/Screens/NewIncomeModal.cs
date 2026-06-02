using CashCtrl.Models;
using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class NewIncomeModal
{
    private static readonly System.Globalization.CultureInfo Br = new("pt-BR");

    // Returns true if an income was saved
    public static async Task<bool> ShowAsync(ControlFile control, decimal currentTotal)
    {
        var periodKey = ControlService.GetCurrentPeriodKey(control);

        decimal amount  = 0m;
        string  date    = DateTime.Now.ToString("dd/MM/yyyy");
        string  origin  = string.Empty;
        int     field   = 0; // 0=amount, 1=date, 2=origin

        while (true)
        {
            DrawModal(currentTotal, amount, date, origin, field);

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape) return false;

            if (key.Key == ConsoleKey.Tab)
            {
                field = (field + 1) % 3;
                continue;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                if (amount <= 0)
                {
                    field = 0;
                    continue;
                }

                var entry = new ControlEntry
                {
                    Date        = date,
                    Total       = amount,
                    Description = origin,
                    Origin      = "income",
                    Details     = new List<EntryItem>()
                };

                await ControlService.SaveIncomeAsync(control, periodKey, entry);

                // Reload period so in-memory totals update
                var reloaded = await ControlService.LoadControlAsync(control.FilePath);
                if (reloaded is not null)
                {
                    control.Periods = reloaded.Periods;
                }

                return true;
            }

            // Inline editing per field
            if (key.Key == ConsoleKey.Backspace)
            {
                if (field == 0 && amount > 0)
                {
                    var s = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "");
                    s = s.Length > 1 ? s[..^1] : "0";
                    amount = decimal.Parse(s) / 100m;
                }
                else if (field == 1 && date.Length > 0)
                    date = date[..^1];
                else if (field == 2 && origin.Length > 0)
                    origin = origin[..^1];
            }
            else if (key.KeyChar >= ' ')
            {
                if (field == 0 && char.IsDigit(key.KeyChar))
                {
                    var s = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "") + key.KeyChar;
                    amount = decimal.Parse(s) / 100m;
                }
                else if (field == 1)
                    date += key.KeyChar;
                else if (field == 2)
                    origin += key.KeyChar;
            }
        }
    }

    private static void DrawModal(decimal currentTotal, decimal amount, string date, string origin, int activeField)
    {
        // Overlay: draw centered modal over current screen
        int w = Math.Max(Console.WindowWidth,  40);
        int h = Math.Max(Console.WindowHeight, 20);

        int mw = Math.Min(64, w - 4);
        int mh = 16;
        int mx = (w - mw) / 2;
        int my = (h - mh) / 2;

        // Dim background by seeking to top and painting the overlay area row by row
        try { Console.SetCursorPosition(mx, my); } catch { }

        var border    = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var dimColor  = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var accentCol = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";

        var lines = BuildModalLines(currentTotal, amount, date, origin, activeField, mw);

        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
            // Pad remainder
            var plain = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "");
            var pad = Math.Max(0, mw - plain.Length);
            Console.Write(new string(' ', pad));
        }

        Console.CursorVisible = false;
    }

    private static List<string> BuildModalLines(
        decimal currentTotal, decimal amount, string date, string origin,
        int activeField, int mw)
    {
        var p   = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var sec = $"#{Theme.Secondary.R:X2}{Theme.Secondary.G:X2}{Theme.Secondary.B:X2}";
        var dim = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var acc = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        var inf = $"#{Theme.Info.R:X2}{Theme.Info.G:X2}{Theme.Info.B:X2}";
        var brd = $"#{110:X2}{100:X2}{160:X2}";

        string Hl(int f, string s) => f == activeField
            ? $"[bold {p}]{Markup.Escape(s)}[/]"
            : $"[{sec}]{Markup.Escape(s)}[/]";

        var totalStr = currentTotal.ToString("C2", new System.Globalization.CultureInfo("pt-BR"));
        var amtStr   = amount.ToString("C2", new System.Globalization.CultureInfo("pt-BR"));

        var inner = mw - 2;
        var top   = $"[{brd}]╭{new string('─', inner)}╮[/]";
        var bot   = $"[{brd}]╰{new string('─', inner)}╯[/]";
        string Row(string content)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(content, @"\[.*?\]", "");
            var pad   = Math.Max(0, inner - plain.Length);
            return $"[{brd}]│[/]{content}{new string(' ', pad)}[{brd}]│[/]";
        }

        // Total display (centered in inner)
        var totalPad = Math.Max(0, (inner - totalStr.Length) / 2);
        var totalLine = Row($"{new string(' ', totalPad)}[bold {acc}]{Markup.Escape(totalStr)}[/]");

        var lines = new List<string>
        {
            top,
            Row(""),
            totalLine,
            Row(""),
            Row($"  [{dim}]Amount added:[/]  {Hl(0, amtStr)}"),
            Row($"  [{dim}]Date:[/]          {Hl(1, date)}"),
            Row($"  [{dim}]Origin:[/]        {Hl(2, string.IsNullOrEmpty(origin) ? "_" : origin)}"),
            Row(""),
            Row(""),
            Row($"  [{dim}]Tab: next field   Enter: save   Esc: cancel[/]"),
            bot,
        };

        return lines;
    }
}

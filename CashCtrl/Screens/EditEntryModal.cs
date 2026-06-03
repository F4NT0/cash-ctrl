using CashCtrl.Models;
using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class EditEntryModal
{
    private static readonly System.Globalization.CultureInfo Br = new("pt-BR");

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static async Task<bool> ShowAsync(
        ControlFile control, string periodKey, string entryKey, ControlEntry entry)
    {
        // Pre-populate fields from existing entry
        string name   = entry.Description ?? entryKey;
        decimal amount = entry.Total;
        string type   = entry.Type ?? string.Empty;
        string date   = entry.Date ?? DateTime.Now.ToString("dd/MM/yyyy");

        int field = 0; // 0=name, 1=amount, 2=type, 3=date

        while (true)
        {
            Draw(name, amount, type, date, field, entry.Origin ?? "expense");

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape) return false;

            if (key.Key == ConsoleKey.Tab)
            {
                field = (field + 1) % 4;
                continue;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                if (string.IsNullOrWhiteSpace(name) || amount <= 0) { field = 0; continue; }

                var updated = new ControlEntry
                {
                    Date        = string.IsNullOrWhiteSpace(date) ? DateTime.Now.ToString("dd/MM/yyyy") : date,
                    Total       = amount,
                    Type        = string.IsNullOrWhiteSpace(type) ? entry.Type : type,
                    TypeColor   = entry.TypeColor,
                    Description = name,
                    Origin      = entry.Origin,
                    Details     = entry.Details,
                };

                await ControlService.UpdateEntryAsync(control, periodKey, entryKey, updated);
                var reloaded = await ControlService.LoadControlAsync(control.FilePath);
                if (reloaded is not null) control.Periods = reloaded.Periods;
                return true;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                switch (field)
                {
                    case 0: if (name.Length   > 0) name   = name[..^1];  break;
                    case 1:
                        var s = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "");
                        s = s.Length > 1 ? s[..^1] : "0";
                        amount = decimal.Parse(s) / 100m;
                        break;
                    case 2: if (type.Length   > 0) type   = type[..^1];  break;
                    case 3: if (date.Length   > 0) date   = date[..^1];  break;
                }
                continue;
            }

            if (field == 0 && key.KeyChar >= ' ')         { name   += key.KeyChar; continue; }
            if (field == 1 && char.IsDigit(key.KeyChar))
            {
                var s = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "") + key.KeyChar;
                amount = decimal.Parse(s) / 100m;
                continue;
            }
            if (field == 2 && key.KeyChar >= ' ')         { type   += key.KeyChar; continue; }
            if (field == 3 && key.KeyChar >= ' ')         { date   += key.KeyChar; continue; }
        }
    }

    private static void Draw(string name, decimal amount, string type, string date, int field, string origin)
    {
        int w   = Math.Max(Console.WindowWidth  > 0 ? Console.WindowWidth  : 80, 40);
        int h   = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 12);
        int mw  = Math.Min(56, w - 4);
        int mh  = 12;
        int mx  = (w - mw) / 2;
        int my  = (h - mh) / 2;

        var brd = "#6E64A0";
        var p   = Hex(Theme.Primary);
        var dim = Hex(Theme.Muted);
        var acc = Hex(Theme.Accent);
        var foc = Hex(Theme.Focus);
        var inner = mw - 2;

        string Hl(bool active, string val) =>
            active
                ? $"[bold {foc}]{Markup.Escape(val.Length > 0 ? val : "")}[/][bold {Hex(Theme.Warning)}]|[/]"
                : $"[{p}]{Markup.Escape(val.Length > 0 ? val : "_")}[/]";

        string Row(string content)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(content, @"\[.*?\]", "");
            var pad   = Math.Max(0, inner - plain.Length);
            return $"[{brd}]│[/]{content}{new string(' ', pad)}[{brd}]│[/]";
        }

        var brPt   = Br;
        var amtStr = amount.ToString("C2", brPt);
        var originColor = origin == "income" ? "#69DB7C" : "#FF6B6B";

        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', inner)}╮[/]",
            Row($"  [{dim}]Edit entry[/]  [{originColor}]{Markup.Escape(origin)}[/]"),
            Row(""),
            Row($"  [{dim}]Name:[/]    {Hl(field == 0, name)}"),
            Row($"  [{dim}]Amount:[/]  {Hl(field == 1, amtStr)}"),
            Row($"  [{dim}]Type:[/]    {Hl(field == 2, type)}"),
            Row($"  [{dim}]Date:[/]    {Hl(field == 3, date)}"),
            Row(""),
            Row($"  [{dim}]Tab: next field   Enter: save   Esc: cancel[/]"),
            Row(""),
            $"[{brd}]╰{new string('─', inner)}╯[/]",
        };

        Console.CursorVisible = false;
        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
            var plain = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "");
            Console.Write(new string(' ', Math.Max(0, mw - plain.Length)));
        }
    }
}

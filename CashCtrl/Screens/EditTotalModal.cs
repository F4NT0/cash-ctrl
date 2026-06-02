using CashCtrl.Models;
using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class EditTotalModal
{
    public static async Task<bool> ShowAsync(ControlFile control, decimal currentTotalValue)
    {
        var periodKey = ControlService.GetCurrentPeriodKey(control);
        decimal value = currentTotalValue;

        while (true)
        {
            DrawModal(currentTotalValue, value);

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape)
                return false;

            if (key.Key == ConsoleKey.Enter)
            {
                await ControlService.SaveTotalValueAsync(control, periodKey, value);
                var reloaded = await ControlService.LoadControlAsync(control.FilePath);
                if (reloaded is not null) control.Periods = reloaded.Periods;
                return true;
            }

            if (key.Key == ConsoleKey.Backspace && value > 0)
            {
                var s = value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "");
                s = s.Length > 1 ? s[..^1] : "0";
                value = decimal.Parse(s) / 100m;
            }
            else if (char.IsDigit(key.KeyChar))
            {
                var s = value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "") + key.KeyChar;
                value = decimal.Parse(s) / 100m;
            }
        }
    }

    private static void DrawModal(decimal currentTotalValue, decimal value)
    {
        int w  = Math.Max(Console.WindowWidth,  40);
        int h  = Math.Max(Console.WindowHeight, 20);
        int mw = Math.Min(52, w - 4);
        int mh = 8;
        int mx = (w - mw) / 2;
        int my = (h - mh) / 2;

        var brd = $"#{110:X2}{100:X2}{160:X2}";
        var p   = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var dim = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var acc = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        var inner = mw - 2;

        var brPt    = new System.Globalization.CultureInfo("pt-BR");
        var curStr  = currentTotalValue.ToString("C2", brPt);
        var valStr  = value.ToString("C2", brPt);

        string Row(string content)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(content, @"\[.*?\]", "");
            var pad   = Math.Max(0, inner - plain.Length);
            return $"[{brd}]│[/]{content}{new string(' ', pad)}[{brd}]│[/]";
        }

        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', inner)}╮[/]",
            Row(""),
            Row($"  [{dim}]Current:[/]  [{acc}]{Markup.Escape(curStr)}[/]"),
            Row($"  [{dim}]New value:[/]  [bold {p}]{Markup.Escape(valStr)}[/]"),
            Row(""),
            Row($"  [{dim}]Enter: save   Esc: cancel[/]"),
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

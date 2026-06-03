using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class CreateControlScreen
{
    private static readonly System.Globalization.CultureInfo BrCulture = new("pt-BR");

    public static async Task ShowAsync(string? defaultName = null, string? workingDirectory = null)
    {
        var cwd    = workingDirectory ?? Directory.GetCurrentDirectory();
        string name = defaultName?.Trim() ?? string.Empty;
        decimal amount = 0m;
        int field  = 0; // 0 = name, 1 = balance
        string error = string.Empty;

        while (true)
        {
            DrawModal(name, amount, field, error, cwd);
            error = string.Empty;

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape) return;

            if (key.Key == ConsoleKey.Tab)
            {
                field = (field + 1) % 2;
                continue;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    field = 0;
                    error = "name cannot be empty";
                    continue;
                }

                var fileSlug = name.Trim().Replace(' ', '-');
                var filePath = Path.Combine(cwd, fileSlug + ".json");

                if (File.Exists(filePath))
                {
                    error = "a control with that name already exists";
                    field = 0;
                    continue;
                }

                await ControlService.CreateControlAsync(filePath, name.Trim(), amount);
                var control = await ControlService.LoadControlAsync(filePath);
                if (control is not null)
                {
                    AnsiConsole.Clear();
                    MainScreen.Show(control);
                }
                return;
            }

            // ── Field editing ────────────────────────────────────────────────
            if (field == 0)
            {
                if (key.Key == ConsoleKey.Backspace)
                { if (name.Length > 0) name = name[..^1]; }
                else if (key.KeyChar >= ' ')
                    name += key.KeyChar;
            }
            else
            {
                if (key.Key == ConsoleKey.Backspace && amount > 0)
                {
                    var s  = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "");
                    s      = s.Length > 1 ? s[..^1] : "0";
                    amount = decimal.Parse(s) / 100m;
                }
                else if (char.IsDigit(key.KeyChar))
                {
                    var s  = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "") + key.KeyChar;
                    amount = decimal.Parse(s) / 100m;
                }
            }
        }
    }

    private static void DrawModal(string name, decimal amount, int field, string error, string directory)
    {
        int w     = Math.Max(Console.WindowWidth,  40);
        int h     = Math.Max(Console.WindowHeight, 20);
        int mw    = Math.Min(62, w - 4);
        int mx    = (w - mw) / 2;
        int inner = mw - 2;

        var brd  = $"#{Theme.Border.R:X2}{Theme.Border.G:X2}{Theme.Border.B:X2}";
        var p    = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var sec  = $"#{Theme.Secondary.R:X2}{Theme.Secondary.G:X2}{Theme.Secondary.B:X2}";
        var dim  = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var acc  = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        var foc  = $"#{Theme.Focus.R:X2}{Theme.Focus.G:X2}{Theme.Focus.B:X2}";
        var red  = "#FF6B6B";
        var info = $"#{Theme.Info.R:X2}{Theme.Info.G:X2}{Theme.Info.B:X2}";

        // Row builder with known plain-text length
        string Row(string mu, int pl)
        {
            int pad = Math.Max(0, inner - pl);
            return $"[{brd}]│[/]{mu}{new string(' ', pad)}[{brd}]│[/]";
        }

        // ── Compute plain widths and truncate before building markup ──────────
        const int prefixLen  = 16; // "  Control name:  " or "  Total money:   "
        const int pathPrefix = 12; // "  Location:   "

        int nameMax = inner - prefixLen - (field == 0 ? 1 : 0);
        var nameFit = name.Length > nameMax && nameMax > 0 ? name[^Math.Max(0, nameMax)..] : name;

        var amtStr  = amount.ToString("C2", BrCulture);
        int amtMax  = inner - prefixLen - (field == 1 ? 1 : 0);
        var amtFit  = amtStr.Length > amtMax && amtMax > 0 ? amtStr[^Math.Max(0, amtMax)..] : amtStr;

        // Path preview
        var fileSlug   = string.IsNullOrWhiteSpace(name) ? "_" : name.Trim().Replace(' ', '-');
        var filePart   = fileSlug + ".json";
        var sep        = Path.DirectorySeparatorChar.ToString();
        int pathBudget = inner - pathPrefix;
        string dirFit, fileFit;
        if (directory.Length + sep.Length + filePart.Length <= pathBudget)
        { dirFit = directory + sep; fileFit = filePart; }
        else
        {
            int dirBudget = pathBudget - filePart.Length;
            dirFit  = dirBudget > 2 ? "…" + (directory + sep)[^(dirBudget - 1)..] : sep;
            fileFit = filePart;
        }
        int pathPlainLen = pathPrefix + dirFit.Length + fileFit.Length;

        // ── Build markup ──────────────────────────────────────────────────────
        var nameLabelMu = field == 0 ? $"[bold {foc}]Control name:[/]" : $"[{dim}]Control name:[/]";
        string nameValMu; int nameValLen;
        if (field == 0)
        { nameValMu = $"[bold {p}]{Markup.Escape(nameFit)}[/][bold {foc}]|[/]"; nameValLen = nameFit.Length + 1; }
        else if (string.IsNullOrWhiteSpace(nameFit))
        { nameValMu = $"[{dim}]_[/]"; nameValLen = 1; }
        else
        { nameValMu = $"[{sec}]{Markup.Escape(nameFit)}[/]"; nameValLen = nameFit.Length; }

        var amtLabelMu = field == 1 ? $"[bold {foc}]Total money: [/]" : $"[{dim}]Total money: [/]";
        string amtValMu; int amtValLen;
        if (field == 1)
        { amtValMu = $"[bold {acc}]{Markup.Escape(amtFit)}[/][bold {foc}]|[/]"; amtValLen = amtFit.Length + 1; }
        else
        { amtValMu = $"[{acc}]{Markup.Escape(amtFit)}[/]"; amtValLen = amtFit.Length; }

        const string hintText = "Tab: next field   Enter: create   Esc: cancel";
        string hintMu; int hintLen;
        if (string.IsNullOrEmpty(error))
        { hintMu = $"  [{dim}]{hintText}[/]"; hintLen = 2 + hintText.Length; }
        else
        {
            var e2 = error.Length > inner - 2 ? error[..(inner - 2)] : error;
            hintMu = $"  [bold {red}]{Markup.Escape(e2)}[/]"; hintLen = 2 + e2.Length;
        }

        // Title row with centered label
        const string titleText = " Creating new Control ";
        int dashL = Math.Max(0, (inner - titleText.Length) / 2);
        int dashR = Math.Max(0, inner - titleText.Length - dashL);

        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', dashL)}[/][bold {foc}]{titleText}[/][{brd}]{new string('─', dashR)}╮[/]",
            Row("", 0),
            Row($"  {nameLabelMu}  {nameValMu}",    prefixLen + nameValLen),
            Row("", 0),
            Row($"  [{dim}]Location:    [/][{dim}]{Markup.Escape(dirFit)}[/][bold {info}]{Markup.Escape(fileFit)}[/]", pathPlainLen),
            Row("", 0),
            Row($"  {amtLabelMu}  {amtValMu}",      prefixLen + amtValLen),
            Row("", 0),
            Row($"  [{brd}]{new string('─', inner - 2)}[/]", inner - 2),
            Row("", 0),
            Row(hintMu, hintLen),
            Row("", 0),
            $"[{brd}]╰{new string('─', inner)}╯[/]",
        };

        int mh = lines.Count;
        int my = Math.Max(0, (h - mh) / 2);

        Console.CursorVisible = false;
        for (int r = 0; r < h; r++)
        {
            try { Console.SetCursorPosition(0, r); } catch { }
            Console.Write(new string(' ', w));
        }

        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
            var plain = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "");
            int leftover = Math.Max(0, w - mx - plain.Length);
            if (leftover > 0) Console.Write(new string(' ', leftover));
        }
    }
}

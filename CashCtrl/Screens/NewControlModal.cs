using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class NewControlModal
{
    private static readonly System.Globalization.CultureInfo Br = new("pt-BR");

    /// <summary>
    /// Shows an inline modal to create a new control file in <paramref name="directory"/>.
    /// Returns the file path of the created control, or null if cancelled.
    /// </summary>
    public static async Task<string?> ShowAsync(string directory)
    {
        string name    = string.Empty;
        decimal amount = 0m;
        int field      = 0; // 0 = name, 1 = balance
        string error   = string.Empty;

        while (true)
        {
            DrawModal(name, amount, field, error, directory);
            error = string.Empty;

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape) return null;

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
                var filePath = Path.Combine(directory, fileSlug + ".json");

                if (File.Exists(filePath))
                {
                    error = "a control with that name already exists";
                    field = 0;
                    continue;
                }

                await ControlService.CreateControlAsync(filePath, name.Trim(), amount);
                return filePath;
            }

            // ── Field editing ────────────────────────────────────────────────
            if (field == 0)
            {
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (name.Length > 0) name = name[..^1];
                }
                else if (key.KeyChar >= ' ')
                {
                    name += key.KeyChar;
                }
            }
            else // field == 1: numeric amount entry (same style as EditTotalModal)
            {
                if (key.Key == ConsoleKey.Backspace && amount > 0)
                {
                    var s = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "");
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
        int mw    = Math.Min(58, w - 4);
        int mx    = (w - mw) / 2;
        int inner = mw - 2; // usable columns between the │ borders

        var brd  = $"#{110:X2}{100:X2}{160:X2}";
        var p    = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var sec  = $"#{Theme.Secondary.R:X2}{Theme.Secondary.G:X2}{Theme.Secondary.B:X2}";
        var dim  = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var acc  = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        var foc  = $"#{Theme.Focus.R:X2}{Theme.Focus.G:X2}{Theme.Focus.B:X2}";
        var red  = "#FF6B6B";
        var info = $"#{Theme.Info.R:X2}{Theme.Info.G:X2}{Theme.Info.B:X2}";

        // Row builder: plainLen is the known plain-text length of markupContent
        string Row(string markupContent, int plainLen)
        {
            int pad = Math.Max(0, inner - plainLen);
            return $"[{brd}]│[/]{markupContent}{new string(' ', pad)}[{brd}]│[/]";
        }

        // ── Plain-text widths ─────────────────────────────────────────────────
        // "  Name   :  " = 12,  "  Initial Amount:  " = 20
        const int namePrefixLen = 12;
        const int amtPrefixLen  = 20;
        const int pathPrefix    = 8;  // "  Path: "

        // Truncate name to fit (cursor █ takes 1 extra col when active)
        int nameMax  = inner - namePrefixLen - (field == 0 ? 1 : 0);
        var nameFit  = name.Length > nameMax && nameMax > 0 ? name[^Math.Max(0, nameMax)..] : name;

        // Amount string
        var amtStr   = amount.ToString("C2", Br);
        int amtMax   = inner - amtPrefixLen - (field == 1 ? 1 : 0);
        var amtFit   = amtStr.Length > amtMax && amtMax > 0 ? amtStr[^Math.Max(0, amtMax)..] : amtStr;

        // Path preview — truncate directory so total fits pathBudget
        var fileSlug  = string.IsNullOrWhiteSpace(name) ? "_" : name.Trim().Replace(' ', '-');
        var filePart  = fileSlug + ".json";
        var sep       = Path.DirectorySeparatorChar.ToString();
        var dirRaw    = directory;
        int pathBudget = inner - pathPrefix;
        string dirFit, fileFit;
        if (dirRaw.Length + sep.Length + filePart.Length <= pathBudget)
        {
            dirFit  = dirRaw + sep;
            fileFit = filePart;
        }
        else
        {
            int dirBudget = pathBudget - filePart.Length;
            dirFit  = dirBudget > 2 ? "…" + (dirRaw + sep)[^(dirBudget - 1)..] : sep;
            fileFit = filePart;
        }
        int pathPlainLen = pathPrefix + dirFit.Length + fileFit.Length;

        // ── Build markup strings ──────────────────────────────────────────────
        var nameLabelMu = field == 0 ? $"[bold {foc}]Name   :[/]" : $"[{dim}]Name   :[/]";
        string nameValMu; int nameValLen;
        if (field == 0)
        { nameValMu = $"[bold {p}]{Markup.Escape(nameFit)}[/][bold {foc}]█[/]"; nameValLen = nameFit.Length + 1; }
        else if (string.IsNullOrWhiteSpace(nameFit))
        { nameValMu = $"[{dim}]_[/]"; nameValLen = 1; }
        else
        { nameValMu = $"[{sec}]{Markup.Escape(nameFit)}[/]"; nameValLen = nameFit.Length; }

        var amtLabelMu = field == 1 ? $"[bold {foc}]Initial Amount:[/]" : $"[{dim}]Initial Amount:[/]";
        string amtValMu; int amtValLen;
        if (field == 1)
        { amtValMu = $"[bold {acc}]{Markup.Escape(amtFit)}[/][bold {foc}]█[/]"; amtValLen = amtFit.Length + 1; }
        else
        { amtValMu = $"[{acc}]{Markup.Escape(amtFit)}[/]"; amtValLen = amtFit.Length; }

        var pathMu = $"[{dim}]{Markup.Escape(dirFit)}[/][bold {info}]{Markup.Escape(fileFit)}[/]";

        const string hintText = "Tab: next field   Enter: create   Esc: cancel";
        string hintMu; int hintLen;
        if (string.IsNullOrEmpty(error))
        { hintMu = $"  [{dim}]{hintText}[/]"; hintLen = 2 + hintText.Length; }
        else
        {
            var e2 = error.Length > inner - 2 ? error[..(inner - 2)] : error;
            hintMu = $"  [bold {red}]{Markup.Escape(e2)}[/]"; hintLen = 2 + e2.Length;
        }

        // ── Assemble lines ────────────────────────────────────────────────────
        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', inner)}╮[/]",
            Row($"  [bold white]New Control[/]",          "  New Control".Length),
            Row("",                                        0),
            Row($"  {nameLabelMu}  {nameValMu}",          namePrefixLen + nameValLen),
            Row($"  {amtLabelMu}  {amtValMu}",            amtPrefixLen  + amtValLen),
            Row("",                                        0),
            Row($"  [{dim}]Path: [/]{pathMu}",            pathPlainLen),
            Row("",                                        0),
            Row(hintMu,                                    hintLen),
            Row("",                                        0),
            $"[{brd}]╰{new string('─', inner)}╯[/]",
        };

        int mh = lines.Count;
        int my = Math.Max(0, (h - mh) / 2);

        Console.CursorVisible = false;
        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
            // Erase any leftover chars to the right (stale redraws)
            int rendered = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "").Length;
            int leftover = Math.Max(0, w - mx - rendered);
            if (leftover > 0) Console.Write(new string(' ', leftover));
        }
    }
}

using CashCtrl.Services;
using Spectre.Console;
using System.Text.Json;

namespace CashCtrl.Screens;

public static class CreateControlScreen
{
    private static readonly System.Globalization.CultureInfo BrCulture = new("pt-BR");
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // ── Entry point ──────────────────────────────────────────────────────────

    public static async Task ShowAsync(string? defaultName = null, string? workingDirectory = null)
    {
        var cwd    = workingDirectory ?? Directory.GetCurrentDirectory();
        string name   = defaultName?.Trim() ?? string.Empty;
        string locDir = ToDisplay(cwd);   // full editable display path (~ prefix)
        decimal amount = 0m;
        int field  = 0; // 0=name  2=amount  (1=location is display-only, skipped)
        string error = string.Empty;

        AnsiConsole.Clear();

        while (true)
        {
            DrawForm(name, locDir, amount, field, error);
            error = string.Empty;

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape || key.KeyChar is 'q' or 'Q')
            {
                AnsiConsole.Clear();
                await WelcomeScreen.ShowAsync();
                return;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                field = field == 0 ? 2 : 0; // skip field 1 (location is display-only)
                continue;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                if (string.IsNullOrWhiteSpace(name))
                { field = 0; error = "Control name cannot be empty"; continue; }

                var realDir  = ToReal(locDir);
                var fileSlug = name.Trim().Replace(' ', '-');
                var filePath = Path.Combine(realDir, fileSlug + ".json");

                if (!Directory.Exists(realDir))
                { field = 1; error = "Directory does not exist"; continue; }

                if (File.Exists(filePath))
                { field = 0; error = "A control with that name already exists here"; continue; }

                // Show confirmation modal
                bool confirmed = await ShowConfirmAsync(name.Trim(), filePath, amount);
                if (!confirmed)
                {
                    AnsiConsole.Clear();
                    await WelcomeScreen.ShowAsync();
                    return;
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

            // ── Field input ───────────────────────────────────────────────────
            if (field == 0)
            {
                if (key.Key == ConsoleKey.Backspace) { if (name.Length > 0) name = name[..^1]; }
                else if (key.KeyChar >= ' ') name += key.KeyChar;
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

    // ── Confirmation modal ────────────────────────────────────────────────────

    // Returns true = confirmed, false = cancelled (back to welcome)
    private static async Task<bool> ShowConfirmAsync(string controlName, string filePath, decimal amount)
    {
        while (true)
        {
            DrawConfirm(controlName, filePath, amount, showPreviewHint: true);

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape || key.KeyChar is 'q' or 'Q')
                return false;

            if (key.Key == ConsoleKey.Enter)
                return true;

            if (key.KeyChar is 'v' or 'V')
            {
                ShowJsonPreview(controlName, amount);
                Console.ReadKey(true); // any key to dismiss
            }

            await Task.Yield();
        }
    }

    private static void DrawConfirm(string controlName, string filePath, decimal amount, bool showPreviewHint)
    {
        int w  = Math.Max(Console.WindowWidth  > 0 ? Console.WindowWidth  : 80, 50);
        int h  = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 12);
        int mw = Math.Min(72, w - 4);

        var foc  = $"#{Theme.Focus.R:X2}{Theme.Focus.G:X2}{Theme.Focus.B:X2}";
        var sec  = $"#{Theme.Secondary.R:X2}{Theme.Secondary.G:X2}{Theme.Secondary.B:X2}";
        var dim  = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var acc  = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        var info = $"#{Theme.Info.R:X2}{Theme.Info.G:X2}{Theme.Info.B:X2}";
        var brd  = "#6E64A0";
        int inner = mw - 2;

        string Row(string content)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(content, @"\[.*?\]", "");
            int pad   = Math.Max(0, inner - plain.Length);
            return $"[{brd}]│[/]{content}{new string(' ', pad)}[{brd}]│[/]";
        }
        string Sep(string l = "├", string r = "┤") =>
            $"[{brd}]{l}{new string('─', inner)}{r}[/]";

        // Display full file path, truncated if too long
        var displayPath = ToDisplay(filePath);
        var fileName    = Path.GetFileName(filePath);
        var dirPart     = Path.GetDirectoryName(displayPath)
                          + Path.DirectorySeparatorChar.ToString();
        // Truncate dir if needed: label "  File  " = 8
        int pathBudget = inner - 8 - fileName.Length;
        string dirShow = dirPart;
        if (dirPart.Length > pathBudget && pathBudget > 3)
            dirShow = "…" + dirPart[^(pathBudget - 1)..];

        var amtStr = amount.ToString("C2", BrCulture);

        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', inner)}╮[/]",
            Row($"  [{dim}]Confirm new Control[/]"),
            Sep(),
            Row($"  [{dim}]Name  [/] [{sec}]{Markup.Escape(controlName)}[/]"),
            Row($"  [{dim}]File  [/] [{dim}]{Markup.Escape(dirShow)}[/][bold {info}]{Markup.Escape(fileName)}[/]"),
            Row($"  [{dim}]Start [/] [{acc}]{Markup.Escape(amtStr)}[/]"),
            Sep(),
            Row($"  [{foc}]Enter[/][{dim}]: create   [/][{foc}]V[/][{dim}]: preview JSON   [/][{foc}]Esc[/][{dim}]: cancel[/]"),
            $"[{brd}]╰{new string('─', inner)}╯[/]",
        };

        int mx = Math.Max(0, (w - mw) / 2);
        int my = Math.Max(0, (h - lines.Count) / 2);

        Console.CursorVisible = false;
        AnsiConsole.Clear();
        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
            var plain = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "");
            Console.Write(new string(' ', Math.Max(0, mw - plain.Length)));
        }
    }

    private static void ShowJsonPreview(string controlName, decimal amount)
    {
        var preview = new Dictionary<string, object>
        {
            ["name"]         = controlName,
            ["total-amount"] = amount
        };
        var json = JsonSerializer.Serialize(preview, new JsonSerializerOptions { WriteIndented = true });

        int w  = Math.Max(Console.WindowWidth  > 0 ? Console.WindowWidth  : 80, 50);
        int h  = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 12);
        int mw = Math.Min(60, w - 4);

        var foc = $"#{Theme.Focus.R:X2}{Theme.Focus.G:X2}{Theme.Focus.B:X2}";
        var dim = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var brd = "#6E64A0";
        int inner = mw - 2;

        string Row(string content)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(content, @"\[.*?\]", "");
            int pad   = Math.Max(0, inner - plain.Length);
            return $"[{brd}]│[/]{content}{new string(' ', pad)}[{brd}]│[/]";
        }
        string Sep(string l = "├", string r = "┤") =>
            $"[{brd}]{l}{new string('─', inner)}{r}[/]";

        // Build JSON lines
        var jsonLines = json.Split('\n').Select(l => l.TrimEnd()).ToArray();

        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', inner)}╮[/]",
            Row($"  [{dim}]JSON Preview[/]"),
            Sep(),
        };
        foreach (var jl in jsonLines)
        {
            var escaped = Markup.Escape(jl.Length > inner - 2 ? jl[..(inner - 2)] : jl);
            lines.Add(Row($"  [{foc}]{escaped}[/]"));
        }
        lines.Add(Sep());
        lines.Add(Row($"  [{dim}]Press any key to go back...[/]"));
        lines.Add($"[{brd}]╰{new string('─', inner)}╯[/]");

        int mx = Math.Max(0, (w - mw) / 2);
        int my = Math.Max(0, (h - lines.Count) / 2);

        AnsiConsole.Clear();
        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
            var plain = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "");
            Console.Write(new string(' ', Math.Max(0, mw - plain.Length)));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ToDisplay(string realPath) =>
        realPath.StartsWith(Home, StringComparison.OrdinalIgnoreCase)
            ? "~" + realPath[Home.Length..]
            : realPath;

    private static string ToReal(string display) =>
        display.StartsWith("~")
            ? Home + display[1..]
            : display;

    // Returns the first N path segments of a display path (~ counts as one segment)
    private static string FirstSegments(string displayPath, int count)
    {
        var sep  = Path.DirectorySeparatorChar;
        var parts = displayPath.Split(sep, StringSplitOptions.None);
        var taken = parts.Take(count).ToArray();
        var result = string.Join(sep, taken);
        // Always append separator at end
        return result.TrimEnd(sep) + sep;
    }

    // ── Form draw ─────────────────────────────────────────────────────────────

    private static void DrawForm(string name, string locDir, decimal amount, int field, string error)
    {
        int w  = Math.Max(Console.WindowWidth  > 0 ? Console.WindowWidth  : 80, 50);
        int h  = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 12);
        int mw = Math.Min(70, w - 4);

        var lines = BuildFormLines(name, locDir, amount, field, error, mw);

        int mx = Math.Max(0, (w - mw) / 2);
        int my = Math.Max(0, (h - lines.Count) / 2);

        Console.CursorVisible = false;
        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
            var plain = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "");
            Console.Write(new string(' ', Math.Max(0, mw - plain.Length)));
        }
    }

    private static List<string> BuildFormLines(string name, string locDir, decimal amount, int field, string error, int mw)
    {
        var foc  = $"#{Theme.Focus.R:X2}{Theme.Focus.G:X2}{Theme.Focus.B:X2}";
        var sec  = $"#{Theme.Secondary.R:X2}{Theme.Secondary.G:X2}{Theme.Secondary.B:X2}";
        var dim  = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var acc  = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        var info = $"#{Theme.Info.R:X2}{Theme.Info.G:X2}{Theme.Info.B:X2}";
        var warn = $"#{Theme.Warning.R:X2}{Theme.Warning.G:X2}{Theme.Warning.B:X2}";
        var red  = "#FF6B6B";
        var brd  = "#6E64A0";
        int inner = mw - 2;

        string Row(string content)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(content, @"\[.*?\]", "");
            int pad   = Math.Max(0, inner - plain.Length);
            return $"[{brd}]│[/]{content}{new string(' ', pad)}[{brd}]│[/]";
        }
        string Sep(string l = "├", string r = "┤") =>
            $"[{brd}]{l}{new string('─', inner)}{r}[/]";

        string Hl(bool active, string val, string color) => active
            ? $"[bold {foc}]{Markup.Escape(val)}[/][bold {warn}]|[/]"
            : $"[{color}]{Markup.Escape(val.Length > 0 ? val : "_")}[/]";

        // Filename derived from name
        var fileSlug = string.IsNullOrWhiteSpace(name) ? "_" : name.Trim().Replace(' ', '-');
        var fileName = fileSlug + ".json";

        // Location: show full path to the .json file
        var locFull    = locDir.Length > 0 ? locDir : "~";
        var dirWithSep = locFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        // Label "  Location        " = 18 chars; remaining budget for path value
        const int locLabel = 18;
        int locBudget = inner - locLabel - 1; // -1 for cursor when active

        string locMu;
        if (field == 1)
        {
            // Active: show editable dir (truncated if needed) + cursor + dim filename
            int dirBudget   = locBudget - fileName.Length;
            string dirDisp  = dirWithSep.Length <= dirBudget ? dirWithSep
                              : dirBudget > 2 ? "…" + dirWithSep[^(dirBudget - 1)..] : dirWithSep;
            locMu = $"[bold {foc}]{Markup.Escape(dirDisp)}[/][bold {warn}]|[/][{dim}]{Markup.Escape(fileName)}[/]";
        }
        else
        {
            // Inactive: show full path (dir + filename), truncated if needed
            var fullPath  = dirWithSep + fileName;
            string pDisp  = fullPath.Length <= locBudget ? fullPath
                            : locBudget > 2 ? "…" + fullPath[^(locBudget - 1)..] : fullPath;
            // Split back into dir / file portions for coloring
            int splitAt   = pDisp.Length - fileName.Length;
            string dPart  = splitAt > 0 ? pDisp[..splitAt] : string.Empty;
            string fPart  = splitAt >= 0 ? pDisp[splitAt..] : pDisp;
            locMu = $"[{sec}]{Markup.Escape(dPart)}[/][{info}]{Markup.Escape(fPart)}[/]";
        }

        var amtStr = amount.ToString("C2", BrCulture);

        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', inner)}╮[/]",
            Row($"  [{dim}]Creating new Control[/]"),
            Sep(),
            Row($"  [{dim}]Control name    [/] {Hl(field == 0, name, sec)}"),
            Row($"  [{dim}]Location        [/] {locMu}"),
            Row($"  [{dim}]Initial amount  [/] {Hl(field == 2, amtStr, acc)}"),
            Sep(),
        };

        if (!string.IsNullOrEmpty(error))
            lines.Add(Row($"  [bold {red}]{Markup.Escape(error)}[/]"));
        else
            lines.Add(Row($"  [{dim}]Tab: next field   Enter: confirm   Esc: cancel[/]"));

        lines.Add($"[{brd}]╰{new string('─', inner)}╯[/]");
        return lines;
    }
}

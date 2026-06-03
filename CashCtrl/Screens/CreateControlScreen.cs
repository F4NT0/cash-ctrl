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

            if (key.Key == ConsoleKey.Escape)
            {
                AnsiConsole.Clear();
                await WelcomeScreen.ShowAsync();
                return;
            }

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
        int w = Math.Max(Console.WindowWidth  > 0 ? Console.WindowWidth  : 80, 40);
        int h = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 10);

        var p    = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var sec  = $"#{Theme.Secondary.R:X2}{Theme.Secondary.G:X2}{Theme.Secondary.B:X2}";
        var dim  = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var acc  = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        var foc  = $"#{Theme.Focus.R:X2}{Theme.Focus.G:X2}{Theme.Focus.B:X2}";
        var brd  = $"#{Theme.Border.R:X2}{Theme.Border.G:X2}{Theme.Border.B:X2}";
        var red  = "#FF6B6B";
        var info = $"#{Theme.Info.R:X2}{Theme.Info.G:X2}{Theme.Info.B:X2}";

        // Neutralize home dir
        var home       = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var displayDir = directory.StartsWith(home, StringComparison.OrdinalIgnoreCase)
                         ? "~" + directory[home.Length..]
                         : directory;

        // Path preview
        var fileSlug  = string.IsNullOrWhiteSpace(name) ? "_" : name.Trim().Replace(' ', '-');
        var filePart  = fileSlug + ".json";
        var pathFull  = displayDir.TrimEnd(Path.DirectorySeparatorChar)
                        + Path.DirectorySeparatorChar + filePart;

        // Field values
        var amtStr = amount.ToString("C2", BrCulture);

        string nameLabelMu = field == 0 ? $"[bold {foc}]Control name:[/]" : $"[{dim}]Control name:[/]";
        string nameValMu   = field == 0
            ? $"[bold {p}]{Markup.Escape(name)}[/][bold {foc}]|[/]"
            : (string.IsNullOrWhiteSpace(name) ? $"[{dim}]_[/]" : $"[{sec}]{Markup.Escape(name)}[/]");

        string amtLabelMu = field == 1 ? $"[bold {foc}]Total money: [/]" : $"[{dim}]Total money: [/]";
        string amtValMu   = field == 1
            ? $"[bold {acc}]{Markup.Escape(amtStr)}[/][bold {foc}]|[/]"
            : $"[{acc}]{Markup.Escape(amtStr)}[/]";

        // Path markup — dim dir, bright filename
        var dirPart  = Path.GetDirectoryName(pathFull) + Path.DirectorySeparatorChar.ToString();
        var fileOnly = Path.GetFileName(pathFull);
        string pathMu = $"[{dim}]{Markup.Escape(dirPart)}[/][bold {info}]{Markup.Escape(fileOnly)}[/]";

        string hintMu = string.IsNullOrEmpty(error)
            ? $"[{dim}]Tab: next field   Enter: create   Esc: cancel[/]"
            : $"[bold {red}]{Markup.Escape(error)}[/]";

        // Lines to render (centered)
        var lines = new List<string>
        {
            $"[bold {foc}]Creating new Control[/]",
            "",
            $"{nameLabelMu}  {nameValMu}",
            "",
            $"[{dim}]Location:    [/]{pathMu}",
            "",
            $"{amtLabelMu}  {amtValMu}",
            "",
            $"[{brd}]{new string('─', Math.Min(50, w - 4))}[/]",
            "",
            hintMu,
        };

        int contentH = lines.Count;
        int startY   = Math.Max(0, (h - contentH) / 2);

        Console.CursorVisible = false;
        AnsiConsole.Clear();

        for (int i = 0; i < lines.Count; i++)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "");
            int pad   = Math.Max(0, (w - plain.Length) / 2);
            try { Console.SetCursorPosition(0, startY + i); } catch { }
            Console.Write(new string(' ', pad));
            AnsiConsole.Markup(lines[i]);
        }
    }
}

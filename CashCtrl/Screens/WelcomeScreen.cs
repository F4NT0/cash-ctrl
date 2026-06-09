using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class WelcomeScreen
{
    private const string MenuOpen   = "Select your control...";
    private const string MenuCreate = "Create new control";

    // ── Public entry points ──────────────────────────────────────────────────

    public static async Task ShowAsync()
    {
        var entries = new[]
        {
            (MenuOpen,   true),
            (MenuCreate, true),
        }.ToList();

        AnsiConsole.Clear();
        var choice = RunSplash(entries);
        AnsiConsole.Clear();

        if (choice is null) return;

        if (choice == MenuOpen)
            await ShowOpenMenuAsync();
        else
            await CreateControlScreen.ShowAsync(workingDirectory: Directory.GetCurrentDirectory());
    }

    public static async Task ShowLocalBrowseAsync(string directory)
    {
        var controls = ControlService.FindControlsInDirectory(directory);

        if (controls.Count == 0)
        {
            AnsiConsole.Clear();
            var choice = RunPanel(
                "Select the control to open",
                new[] { ("Create new control here", true), ("Exit", true) }.ToList(),
                notice: $"No controls found in {directory}");

            AnsiConsole.Clear();
            if (choice == "Create new control here")
                await CreateControlScreen.ShowAsync(workingDirectory: directory);
            return;
        }

        var names = controls.Select(f => Path.GetFileNameWithoutExtension(f)!).ToList();

        AnsiConsole.Clear();
        var selected = RunPanel("Select the control to open", names.Select(n => (n, true)).ToList());
        AnsiConsole.Clear();

        if (selected is null) { await ShowAsync(); return; }

        var filePath = controls[names.IndexOf(selected)];
        await OpenControlScreen.ShowAsync(filePath);
    }

    // ── Open menu ────────────────────────────────────────────────────────────

    private static async Task ShowOpenMenuAsync()
    {
        var favorites = ControlService.GetFavorites()
            .Where(f => File.Exists(f.FilePath))
            .Take(15)
            .ToList();

        var local = ControlService.FindControlsInDirectory(Directory.GetCurrentDirectory())
            .Where(p => !favorites.Any(f => string.Equals(f.FilePath, p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (favorites.Count == 0 && local.Count == 0)
        {
            AnsiConsole.Clear();
            var fallback = RunPanel(
                "Select the control to open",
                new[] { ("Create new control", true)}.ToList(),
                notice: "No saved controls found.");
            AnsiConsole.Clear();

            if (fallback == "Create new control")
                await CreateControlScreen.ShowAsync(workingDirectory: Directory.GetCurrentDirectory());
            return;
        }

        var entries = new List<(string label, bool selectable)>();
        var fileMap = new Dictionary<string, string>();
        var favMap  = new Dictionary<string, FavoriteEntry>();

        if (favorites.Count > 0)
        {
            entries.Add(("Latest controls", false));
            foreach (var fav in favorites)
            {
                var label = fav.Name;
                entries.Add((label, true));
                favMap[label] = fav;
            }
        }

        if (local.Count > 0)
        {
            entries.Add(("Local directory", false));
            foreach (var p in local)
            {
                var label = Path.GetFileNameWithoutExtension(p)!;
                entries.Add((label, true));
                fileMap[label] = p;
            }
        }

        AnsiConsole.Clear();
        var selected = RunPanel("Select the control to open", entries);
        AnsiConsole.Clear();

        if (selected is null) { await ShowAsync(); return; }

        if (favMap.TryGetValue(selected, out var favEntry))
        {
            await OpenControlScreen.ShowAsync(favEntry.FilePath);
            return;
        }

        if (fileMap.TryGetValue(selected, out var localPath))
            await OpenControlScreen.ShowAsync(localPath);
    }

    // ── Splash renderer ──────────────────────────────────────────────────────
    //
    // Static header is drawn ONCE. Only the menu+hints block is redrawn in-place
    // on each keypress, eliminating figlet flicker.

    private static string? RunSplash(
        List<(string label, bool selectable)> entries,
        string? notice = null)
    {
        int idx = entries.FindIndex(e => e.selectable);
        if (idx < 0) return null;

        int w = Math.Max(Console.WindowWidth  > 0 ? Console.WindowWidth  : 80, 20);
        int h = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 6);

        // ── measure heights ───────────────────────────────────────────────
        int figletLines    = FigletHeight(w);
        int subtitleRows   = 1;
        int descRows       = w >= DescLine.Length + 4 ? 1 : 2;
        int blankAfterDesc = 2;
        int ruleRows       = 1;
        int blankAfterRule = 1;
        int versionRows    = 1; // version label or update banner (in dynamic block, above menu)
        int noticeRows     = notice is null ? 0 : 2;
        int menuRows       = entries.Count + 1; // +1 leading blank
        int hintsRows      = 2;

        int staticH  = figletLines + subtitleRows + descRows + blankAfterDesc
                       + ruleRows + blankAfterRule + noticeRows;
        int dynamicH = versionRows + menuRows + hintsRows;
        int contentH = staticH + dynamicH;
        int topPad   = Math.Max(0, (h - contentH) / 2);

        // ── Draw static header once ───────────────────────────────────────
        AnsiConsole.Clear();
        for (int i = 0; i < topPad; i++) Console.WriteLine();
        DrawStaticHeader(w, notice);

        // Remember the row where the dynamic block starts (includes version row)
        int menuTop = Console.CursorTop;

        // Pre-fill dynamic area so first redraw doesn't scroll
        var blankLine = new string(' ', w);
        for (int i = 0; i < dynamicH; i++) Console.WriteLine(blankLine);

        // ── Interactive loop — only redraws version+menu+hints ──────────────
        while (true)
        {
            DrawDynamic(entries, idx, w, menuTop);

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape) return null;
            if (key.KeyChar is 'q' or 'Q')     return null;
            if (key.Key == ConsoleKey.Enter)  return entries[idx].label;

            if (key.Key == ConsoleKey.UpArrow)
            {
                int prev = idx - 1;
                while (prev >= 0 && !entries[prev].selectable) prev--;
                if (prev >= 0) idx = prev;
            }
            else if (key.Key == ConsoleKey.DownArrow)
            {
                int next = idx + 1;
                while (next < entries.Count && !entries[next].selectable) next++;
                if (next < entries.Count) idx = next;
            }
        }
    }

    // ── Static header (drawn once) ───────────────────────────────────────────

    private static void DrawStaticHeader(int w, string? notice)
    {
        // Figlet
        if (w >= FigletMinWidth)
            AnsiConsole.Write(new FigletText(GetFont(), "CASH-CTRL").Centered().Color(Theme.Primary));
        else if (w >= SmallFigletMin)
            AnsiConsole.Write(new FigletText("CASH-CTRL").Centered().Color(Theme.Primary));
        else
        {
            WriteCentered($"[bold #{C(Theme.Primary)}]CASH-CTRL[/]", w);
            Console.WriteLine();
        }

        // Subtitle
        WriteCentered($"[bold #{C(Theme.Primary)}]{Markup.Escape(Subtitle)}[/]", w);
        Console.WriteLine();

        // Description
        if (w >= DescLine.Length + 4)
            WriteCentered($"[#{C(Theme.Muted)}]{Markup.Escape(DescLine)}[/]", w);
        else
        {
            WriteCentered($"[#{C(Theme.Muted)}]{Markup.Escape(DescA)}[/]", w);
            Console.WriteLine();
            WriteCentered($"[#{C(Theme.Muted)}]{Markup.Escape(DescB)}[/]", w);
        }

        Console.WriteLine();
        Console.WriteLine();

        // Rule
        AnsiConsole.Write(new Rule { Style = new Style(Theme.Border) });
        Console.WriteLine();

        // Optional notice
        if (notice is not null)
        {
            Console.WriteLine();
            WriteCentered($"[#{C(Theme.Muted)}]{Markup.Escape(notice)}[/]", w);
        }
    }

    // ── Dynamic menu + hints (redrawn in-place, no flicker) ──────────────────

    private static void DrawDynamic(
        List<(string label, bool selectable)> entries,
        int selected,
        int w,
        int topRow)
    {
        try { Console.SetCursorPosition(0, topRow); } catch { }

        var blankLine = new string(' ', w);

        // Version label or update banner (redrawn on every keypress)
        Console.Write(blankLine);
        try { Console.SetCursorPosition(0, topRow); } catch { }
        if (CashCtrl.Services.VersionService.IsOutdated)
            WriteCentered($"[bold yellow]\u26a0 New version available ({CashCtrl.Services.VersionService.LatestVersion}), run cash-ctrl --update to update[/]", w);
        else
            WriteCentered($"[#{C(Theme.Muted)}]{CashCtrl.AppVersion.Current}[/]", w);

        // Menu items (offset by +1 for the version row)
        for (int i = 0; i < entries.Count; i++)
        {
            var (label, selectable) = entries[i];
            string rendered;

            if (!selectable)
                rendered = $"[#{C(Theme.Muted)}]{Markup.Escape(label)}[/]";
            else if (i == selected)
                rendered = $"[bold #{C(Theme.Primary)}]> {Markup.Escape(label)}[/]";
            else
                rendered = $"[#{C(Theme.Secondary)}]{Markup.Escape(label)}[/]";

            // Clear the line first, then write centered
            Console.Write(blankLine);
            try { Console.SetCursorPosition(0, topRow + 1 + i); } catch { }
            WriteCentered(rendered, w);
        }

        // Blank separator + hints (offset by +1 for version row)
        Console.Write(blankLine);
        Console.WriteLine();
        Console.Write(blankLine);
        try { Console.SetCursorPosition(0, topRow + entries.Count + 3); } catch { }
        WriteCentered($"[#{C(Theme.Muted)}]↑↓: select | Enter: confirm | esc: quit[/]", w);

        // Park cursor off-screen to prevent blinking on last line
        try { Console.SetCursorPosition(0, topRow + entries.Count + 3); } catch { }
    }

    // ── Centered panel selector (used for open-control flows) ────────────────

    private static string? RunPanel(
        string title,
        List<(string label, bool selectable)> entries,
        string? notice = null)
    {
        int idx = entries.FindIndex(e => e.selectable);
        if (idx < 0) return null;

        while (true)
        {
            DrawPanelSelector(title, entries, idx, notice);

            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Escape) return null;
            if (key.KeyChar is 'q' or 'Q')   return null;
            if (key.Key == ConsoleKey.Enter)  return entries[idx].label;

            if (key.Key == ConsoleKey.UpArrow)
            {
                int prev = idx - 1;
                while (prev >= 0 && !entries[prev].selectable) prev--;
                if (prev >= 0) idx = prev;
            }
            else if (key.Key == ConsoleKey.DownArrow)
            {
                int next = idx + 1;
                while (next < entries.Count && !entries[next].selectable) next++;
                if (next < entries.Count) idx = next;
            }
        }
    }

    private static void DrawPanelSelector(
        string title,
        List<(string label, bool selectable)> entries,
        int selected,
        string? notice)
    {
        int w  = Math.Max(Console.WindowWidth  > 0 ? Console.WindowWidth  : 80, 24);
        int h  = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 8);

        // Panel width: wide enough for the longest entry + padding, max 90
        int maxLabel = entries.Max(e => e.label.Length);
        int pw  = Math.Min(90, Math.Max(48, maxLabel + 10));
        pw      = Math.Min(pw, w - 4);
        int inner = pw - 2;
        int px  = Math.Max(0, (w - pw) / 2);

        var brd  = $"#{C(Theme.Border)}";
        var foc  = $"#{C(Theme.Focus)}";
        var dim  = $"#{C(Theme.Muted)}";
        var pri  = $"#{C(Theme.Primary)}";
        var sec  = $"#{C(Theme.Secondary)}";
        var warn = $"#{C(Theme.Warning)}";

        string Row(string markupContent, int plainLen)
        {
            int pad = Math.Max(0, inner - plainLen);
            return $"[{brd}]│[/]{markupContent}{new string(' ', pad)}[{brd}]│[/]";
        }

        var lines = new List<string>();
        // Title border
        var titlePlain = $" {title} ";
        int dashL = Math.Max(0, (inner - titlePlain.Length) / 2);
        int dashR = Math.Max(0, inner - titlePlain.Length - dashL);
        lines.Add($"[{brd}]╭{new string('─', dashL)}[/][bold {foc}]{Markup.Escape(titlePlain)}[/][{brd}]{new string('─', dashR)}╮[/]");
        lines.Add(Row("", 0));

        if (notice is not null)
        {
            var n2 = notice.Length > inner - 2 ? notice[..(inner - 2)] : notice;
            lines.Add(Row($"  [{dim}]{Markup.Escape(n2)}[/]", 2 + n2.Length));
            lines.Add(Row("", 0));
        }

        foreach (var (label, selectable) in entries)
        {
            string mu; int pl;
            if (!selectable)
            {
                var lbl = label.Length > inner - 2 ? label[..(inner - 2)] : label;
                mu = $"  [{dim}]{Markup.Escape(lbl)}[/]"; pl = 2 + lbl.Length;
            }
            else if (entries.IndexOf((label, selectable)) == selected)
            {
                var lbl = label.Length > inner - 4 ? label[..(inner - 4)] : label;
                mu = $"  [bold {pri}]> {Markup.Escape(lbl)}[/]"; pl = 4 + lbl.Length;
            }
            else
            {
                var lbl = label.Length > inner - 4 ? label[..(inner - 4)] : label;
                mu = $"  [{sec}]  {Markup.Escape(lbl)}[/]"; pl = 4 + lbl.Length;
            }
            lines.Add(Row(mu, pl));
        }

        lines.Add(Row("", 0));
        const string hint = "↑↓: select | Enter: confirm | Esc: back";
        var hintTrunc = hint.Length > inner - 2 ? hint[..(inner - 2)] : hint;
        lines.Add(Row($"  [{dim}]{Markup.Escape(hintTrunc)}[/]", 2 + hintTrunc.Length));
        lines.Add(Row("", 0));
        lines.Add($"[{brd}]╰{new string('─', inner)}╯[/]");

        int mh = lines.Count;
        int my = Math.Max(0, (h - mh) / 2);

        Console.CursorVisible = false;
        // Clear screen
        Console.SetCursorPosition(0, 0);
        for (int r = 0; r < h; r++)
        {
            try { Console.SetCursorPosition(0, r); } catch { }
            Console.Write(new string(' ', w));
        }

        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(px, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private const int FigletMinWidth = 90;
    private const int SmallFigletMin = 40;
    private const string Subtitle = "Command-line finance control software";
    private const string DescLine = "Track expenses and income  ·  store as JSON  ·  open from anywhere";
    private const string DescA    = "Track expenses and income";
    private const string DescB    = "store as JSON  ·  open from anywhere";

    // Shorthand hex color
    private static string C(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";

    // ANSI Shadow font — loaded once from embedded resource
    private static FigletFont? _figletFont;
    private static FigletFont GetFont()
    {
        if (_figletFont is not null) return _figletFont;
        var asm      = typeof(WelcomeScreen).Assembly;
        var resName  = asm.GetManifestResourceNames()
                          .First(n => n.EndsWith("ansi-shadow.flf", StringComparison.OrdinalIgnoreCase));
        using var s  = asm.GetManifestResourceStream(resName)!;
        _figletFont  = FigletFont.Load(s);
        return _figletFont;
    }

    // How many terminal lines the figlet block occupies
    private static int FigletHeight(int w)
    {
        if (w >= FigletMinWidth) return 6;   // ANSI Shadow is 6 lines tall
        if (w >= SmallFigletMin) return 6;   // default figlet is also 6
        return 2;                             // plain text + blank
    }

    /// <summary>
    /// Writes markup centered within <paramref name="width"/> columns.
    /// Strips tags to compute visible length.
    /// </summary>
    private static void WriteCentered(string markup, int width)
    {
        var plain = System.Text.RegularExpressions.Regex.Replace(markup, @"\[.*?\]", "");
        int pad   = Math.Max(0, (width - plain.Length) / 2);
        AnsiConsole.Markup(new string(' ', pad) + markup);
        Console.WriteLine();
    }
}

using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class WelcomeScreen
{
    private const string MenuOpen   = "Open controls...";
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
            var choice = RunSplash(new[]
            {
                ("Create new control here", true),
                ("Exit",                   true),
            }.ToList(), notice: $"No controls found in {directory}");

            AnsiConsole.Clear();
            if (choice == "Create new control here")
                await CreateControlScreen.ShowAsync(workingDirectory: directory);
            return;
        }

        var names = controls.Select(f => Path.GetFileNameWithoutExtension(f)!).ToList();
        names.Add("← Back");

        AnsiConsole.Clear();
        var selected = RunSplash(names.Select(n => (n, true)).ToList());
        AnsiConsole.Clear();

        if (selected == "← Back" || selected is null) return;

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
            var fallback = RunSplash(new[]
            {
                ("Create new control", true),
                ("← Back",            true),
            }.ToList(), notice: "No saved controls found.");
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
            entries.Add(("Recent", false));
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

        entries.Add(("← Back", true));

        AnsiConsole.Clear();
        var selected = RunSplash(entries);
        AnsiConsole.Clear();

        if (selected is null || selected.Trim() == "← Back")
        {
            await ShowAsync();
            return;
        }

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
        int noticeRows     = notice is null ? 0 : 2;
        int menuRows       = entries.Count + 1; // +1 leading blank
        int hintsRows      = 2;

        int staticH  = figletLines + subtitleRows + descRows + blankAfterDesc
                       + ruleRows + blankAfterRule + noticeRows;
        int dynamicH = menuRows + hintsRows;
        int contentH = staticH + dynamicH;
        int topPad   = Math.Max(0, (h - contentH) / 2);

        // ── Draw static header once ───────────────────────────────────────
        AnsiConsole.Clear();
        for (int i = 0; i < topPad; i++) Console.WriteLine();
        DrawStaticHeader(w, notice);

        // Remember the row where the dynamic block starts
        int menuTop = Console.CursorTop;

        // Pre-fill dynamic area so first redraw doesn't scroll
        var blankLine = new string(' ', w);
        for (int i = 0; i < dynamicH; i++) Console.WriteLine(blankLine);

        // ── Interactive loop — only redraws menu+hints ────────────────────
        while (true)
        {
            DrawDynamic(entries, idx, w, menuTop);

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape) return null;
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

        // Leading blank
        Console.Write(blankLine);
        Console.WriteLine();

        // Menu items
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

        // Blank separator + hints
        Console.Write(blankLine);
        Console.WriteLine();
        Console.Write(blankLine);
        try { Console.SetCursorPosition(0, topRow + entries.Count + 2); } catch { }
        WriteCentered($"[#{C(Theme.Muted)}]↑↓: select    enter: confirm    esc: quit[/]", w);

        // Park cursor off-screen to prevent blinking on last line
        try { Console.SetCursorPosition(0, topRow + entries.Count + 2); } catch { }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private const int FigletMinWidth = 90;
    private const int SmallFigletMin = 40;
    private const string Subtitle = "Terminal-native personal finance control";
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

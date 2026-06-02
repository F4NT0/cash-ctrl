using CashCtrl.Models;
using CashCtrl.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace CashCtrl.Screens;

// ── Focus panels ─────────────────────────────────────────────────────────────
public enum MainFocus { None, Controls, Chart, Calendar, Total, Expenses }

public static class MainScreen
{
    private static readonly System.Globalization.CultureInfo Br = new("pt-BR");

    // ── Color helpers ─────────────────────────────────────────────────────────
    private static Style BorderStyle(bool focused) =>
        new(focused ? Theme.Warning : Theme.Border);

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    // ── Entry point ───────────────────────────────────────────────────────────
    public static void Show(ControlFile control) => ShowAsync(control).GetAwaiter().GetResult();

    public static async Task ShowAsync(ControlFile control)
    {
        var focus       = MainFocus.None;
        var controlsDir = Path.GetDirectoryName(control.FilePath) ?? Directory.GetCurrentDirectory();
        var siblingControls = ControlService.FindControlsInDirectory(controlsDir);
        int controlsIdx = siblingControls.IndexOf(control.FilePath);
        if (controlsIdx < 0) controlsIdx = 0;

        while (true)
        {
            // Reload from disk to pick up saved entries
            var refreshed = await ControlService.LoadControlAsync(control.FilePath);
            if (refreshed is not null) control = refreshed;
            siblingControls = ControlService.FindControlsInDirectory(controlsDir);

            DrawAll(control, siblingControls, controlsIdx, focus);

            var k = await Task.Run(() => Console.ReadKey(true));

            if (k.Key == ConsoleKey.Escape) { AnsiConsole.Clear(); return; }

            if (k.KeyChar is 'o' or 'O')
                focus = focus == MainFocus.Controls ? MainFocus.None : MainFocus.Controls;

            if (k.KeyChar is 't' or 'T')
            {
                focus = MainFocus.Total;
                DrawAll(control, siblingControls, controlsIdx, focus);
                var period = control.Periods.Values.FirstOrDefault();
                var currentTotal = (period?.TotalValue ?? 0m) + (period?.TotalIncome ?? 0m);
                if (await NewIncomeModal.ShowAsync(control, currentTotal))
                {
                    var r = await ControlService.LoadControlAsync(control.FilePath);
                    if (r is not null) control = r;
                }
            }

            if (k.KeyChar is 'e' or 'E')
            {
                focus = MainFocus.Expenses;
                DrawAll(control, siblingControls, controlsIdx, focus);
                if (await NewExpenseModal.ShowAsync(control))
                {
                    var r = await ControlService.LoadControlAsync(control.FilePath);
                    if (r is not null) control = r;
                }
            }

            if (focus == MainFocus.Controls)
            {
                if (k.Key == ConsoleKey.UpArrow && controlsIdx > 0) controlsIdx--;
                if (k.Key == ConsoleKey.DownArrow && controlsIdx < siblingControls.Count - 1) controlsIdx++;
                if (k.Key == ConsoleKey.Enter && siblingControls.Count > 0)
                {
                    var nc = await ControlService.LoadControlAsync(siblingControls[controlsIdx]);
                    if (nc is not null)
                    {
                        control     = nc;
                        controlsDir = Path.GetDirectoryName(control.FilePath) ?? controlsDir;
                    }
                }
            }
        }
    }

    // ── Main draw — Spectre Layout (full-screen, responsive) ──────────────────
    private static void DrawAll(
        ControlFile control,
        List<string> siblings,
        int controlsIdx,
        MainFocus focus)
    {
        Console.CursorVisible = false;
        // Move to top-left instead of clearing to avoid flicker
        Console.SetCursorPosition(0, 0);

        var period = control.Periods.Values.FirstOrDefault();
        var totalMoney = (period?.TotalValue ?? 0m) + (period?.TotalIncome ?? 0m);
        var totalExp   = period?.TotalExpenses ?? 0m;
        var difference = totalMoney - totalExp;

        // ── Build top panels ──────────────────────────────────────────────────
        var controlsPanel = MakeControlsPanel(siblings, controlsIdx, control.FilePath, focus == MainFocus.Controls);
        var chartPanel    = MakeChartPanel(period, focus == MainFocus.Chart);
        var calPanel      = MakeCalendarPanel(period, focus == MainFocus.Calendar);

        // ── Build middle summary row (compact inline values + clock) ──────────
        var monLabel  = MakeSummaryPanel("Total money",    FormatMoney(totalMoney), Theme.Accent,           focus == MainFocus.Total);
        var expLabel  = MakeSummaryPanel("Total expenses", FormatMoney(totalExp),   new Color(255, 80, 80), focus == MainFocus.Expenses);
        var diffLabel = MakeSummaryPanel("Difference",     FormatMoney(difference),
                                         difference >= 0 ? Theme.Accent : new Color(255, 80, 80), false);
        var clockLabel = MakeSummaryClockPanel();

        // ── Build bottom entry list ────────────────────────────────────────────
        var listPanel = MakeEntryListPanel(period, focus == MainFocus.Expenses);

        // ── Spectre Layout ────────────────────────────────────────────────────
        // Structure:
        //  layout["root"]
        //   ├─ layout["top"]    (60%)
        //   │   ├─ layout["controls"]  (20%)
        //   │   ├─ layout["chart"]     (55%)
        //   │   └─ layout["calendar"]  (25%)
        //   ├─ layout["mid"]    (15%)
        //   │   ├─ layout["total"]     (25%)
        //   │   ├─ layout["expenses"]  (25%)
        //   │   ├─ layout["diff"]      (25%)
        //   │   └─ layout["clock"]     (25%)
        //   └─ layout["list"]   (25%)

        // Reserve 1 line for the footer status bar
        int availableHeight = Math.Max(9, Console.WindowHeight - 1);

        var layout = new Layout("root")
        {
            Size = availableHeight
        };
        layout.SplitRows(
            new Layout("top").Ratio(1)
                .SplitColumns(
                    new Layout("controls").Ratio(1),
                    new Layout("chart").Ratio(1),
                    new Layout("calendar").Ratio(1)
                ),
            new Layout("mid").Ratio(1)
                .SplitColumns(
                    new Layout("total").Ratio(1),
                    new Layout("exptotal").Ratio(1),
                    new Layout("diff").Ratio(1),
                    new Layout("clock").Ratio(1)
                ),
            new Layout("list").Ratio(1)
        );

        layout["controls"].Update(controlsPanel);
        layout["chart"].Update(chartPanel);
        layout["calendar"].Update(calPanel);
        layout["total"].Update(monLabel);
        layout["exptotal"].Update(expLabel);
        layout["diff"].Update(diffLabel);
        layout["clock"].Update(clockLabel);
        layout["list"].Update(listPanel);

        AnsiConsole.Write(layout);

        // Status bar — always on the reserved last line
        var footer = $" [bold {Hex(Theme.Warning)}](O)[/][{Hex(Theme.Muted)}] controls   [/]" +
                     $"[bold {Hex(Theme.Warning)}](T)[/][{Hex(Theme.Muted)}] new income   [/]" +
                     $"[bold {Hex(Theme.Warning)}](E)[/][{Hex(Theme.Muted)}] new expense   [/]" +
                     $"[bold {Hex(Theme.Warning)}](G)[/][{Hex(Theme.Muted)}] chart   [/]" +
                     $"[bold {Hex(Theme.Warning)}](C)[/][{Hex(Theme.Muted)}] calendar   [/]" +
                     $"[bold {Hex(Theme.Warning)}]Esc[/][{Hex(Theme.Muted)}] quit[/]";
        AnsiConsole.Markup(footer);
    }

    // ── Panel builders ────────────────────────────────────────────────────────

    private static Panel MakeControlsPanel(
        List<string> siblings, int selectedIdx, string currentPath, bool focused)
    {
        var rows = new List<string>();
        foreach (var (fp, i) in siblings.Select((f, i) => (f, i)))
        {
            var name      = GetDisplayName(fp);
            var isCurrent = string.Equals(fp, currentPath, StringComparison.OrdinalIgnoreCase);
            var isSel     = i == selectedIdx;
            string line;
            if (isCurrent && isSel)
                line = $"[bold {Hex(Theme.Warning)}]> {Markup.Escape(name)}[/]";
            else if (isCurrent)
                line = $"[{Hex(Theme.Primary)}]  {Markup.Escape(name)}[/]";
            else if (isSel)
                line = $"[bold {Hex(Theme.Secondary)}]> {Markup.Escape(name)}[/]";
            else
                line = $"[{Hex(Theme.Muted)}]  {Markup.Escape(name)}[/]";
            rows.Add(line);
        }

        var content = rows.Count > 0
            ? (IRenderable)new Markup(string.Join("\n", rows))
            : new Markup($"[{Hex(Theme.Muted)}](empty)[/]");

        return MakePanel(content, "Controls", focused);
    }

    private static Panel MakeChartPanel(ControlPeriod? period, bool focused)
    {
        var byType = new Dictionary<string, (decimal total, string color)>();
        if (period is not null)
        {
            foreach (var e in period.Entries.Values.Where(e => e.Origin == "expense"))
            {
                var t = e.Type ?? "Other";
                var c = e.TypeColor ?? "AAAAAA";
                byType.TryAdd(t, (0m, c));
                byType[t] = (byType[t].total + e.Total, byType[t].color);
            }
        }

        IRenderable content;
        if (byType.Count == 0)
        {
            content = new Markup($"[{Hex(Theme.Muted)}](no expenses yet)[/]");
        }
        else
        {
            var maxVal   = byType.Values.Max(v => v.total);
            var types    = byType.OrderByDescending(kv => kv.Value.total).ToList();
            int maxLabel = Math.Min(types.Max(kv => kv.Key.Length), 14);
            int barMax   = Math.Max(1, 40);

            var sb = new System.Text.StringBuilder();
            foreach (var (typeName, (val, colHex)) in types)
            {
                var label  = typeName.Length > maxLabel ? typeName[..maxLabel] : typeName.PadRight(maxLabel);
                int barLen = maxVal > 0 ? Math.Max(1, (int)((val / maxVal) * barMax)) : 1;
                var bar    = new string('█', barLen);
                var valStr = FormatMoney(val);
                sb.AppendLine(
                    $"[{Hex(Theme.Muted)}]{Markup.Escape(label)}[/] " +
                    $"[#{colHex}]{bar}[/] " +
                    $"[{Hex(Theme.Muted)}]{Markup.Escape(valStr)}[/]");
            }
            content = new Markup(sb.ToString().TrimEnd());
        }

        return MakePanel(content, "Expense types", focused);
    }

    private static Panel MakeCalendarPanel(ControlPeriod? period, bool focused)
    {
        var dayMap = new Dictionary<int, (bool exp, bool inc)>();
        if (period is not null)
        {
            foreach (var e in period.Entries.Values)
            {
                if (DateTime.TryParseExact(e.Date, "dd/MM/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
                {
                    var cur = dayMap.GetValueOrDefault(dt.Day);
                    dayMap[dt.Day] = (cur.exp || e.Origin == "expense",
                                      cur.inc || e.Origin == "income");
                }
            }
        }

        var now         = DateTime.Now;
        var firstDay    = new DateTime(now.Year, now.Month, 1);
        int startDow    = (int)firstDay.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[bold {Hex(Theme.Secondary)}]{now:MMMM yyyy}[/]");
        sb.AppendLine($"[{Hex(Theme.Muted)}]Su Mo Tu We Th Fr Sa[/]");

        for (int week = 0; week < 6; week++)
        {
            bool anyDay = false;
            var weekSb = new System.Text.StringBuilder();
            for (int col = 0; col < 7; col++)
            {
                int d = week * 7 + col - startDow + 1;
                if (d < 1 || d > daysInMonth)
                {
                    weekSb.Append("   ");
                }
                else
                {
                    anyDay = true;
                    var hasE = dayMap.TryGetValue(d, out var dm) && dm.exp;
                    var hasI = dayMap.ContainsKey(d) && dayMap[d].inc;
                    var col2 = (hasE && hasI) ? $"{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}"
                             : hasE           ? "FF6B6B"
                             : hasI           ? "69DB7C"
                             :                  $"{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
                    weekSb.Append($"[#{col2}]{d,2}[/] ");
                }
            }
            if (!anyDay) break;
            sb.AppendLine(weekSb.ToString().TrimEnd());
        }

        return MakePanel(new Markup(sb.ToString().TrimEnd()), "Calendar", focused);
    }

    private static Panel MakeSummaryPanel(string title, string value, Color valueColor, bool focused)
    {
        var content = Align.Center(
            new Markup($"[bold {Hex(valueColor)}]{Markup.Escape(value)}[/]"));
        return MakePanel(content, title, focused, Justify.Center);
    }

    private static Panel MakeSummaryClockPanel()
    {
        var time    = DateTime.Now.ToString("HH:mm:ss");
        var content = Align.Center(
            new Markup($"[bold {Hex(Theme.Primary)}]{time}[/]"));
        return MakePanel(content, "Clock", false, Justify.Center);
    }

    private static Panel MakeEntryListPanel(ControlPeriod? period, bool focused)
    {
        var entries = period?.Entries.Values.OrderByDescending(e => e.Date).ToList()
                      ?? new List<ControlEntry>();

        IRenderable content;
        if (entries.Count == 0)
        {
            content = new Markup($"[{Hex(Theme.Muted)}](no entries yet)[/]");
        }
        else
        {
            var table = new Table();
            table.Expand();
            table.Border(TableBorder.Rounded);
            table.BorderColor(Theme.Border);

            table.AddColumn(new TableColumn($"[{Hex(Theme.Muted)}]Date[/]"));
            table.AddColumn(new TableColumn($"[{Hex(Theme.Muted)}]Description[/]"));
            table.AddColumn(new TableColumn($"[{Hex(Theme.Muted)}]Type[/]"));
            table.AddColumn(new TableColumn($"[{Hex(Theme.Muted)}]Amount[/]").RightAligned());
            table.AddColumn(new TableColumn($"[{Hex(Theme.Muted)}]Origin[/]").Centered());

            foreach (var e in entries)
            {
                var isIncome    = e.Origin == "income";
                var amtColor    = isIncome ? "69DB7C" : "FF6B6B";
                var originLabel = isIncome
                    ? $"[#69DB7C]income[/]"
                    : $"[#FF6B6B]expense[/]";
                var typeHex     = e.TypeColor is { Length: > 0 } tc ? $"#{tc}" : Hex(Theme.Muted);
                table.AddRow(
                    new Markup($"[{Hex(Theme.Muted)}]{Markup.Escape(e.Date)}[/]"),
                    new Markup($"[{Hex(Theme.Secondary)}]{Markup.Escape(e.Description ?? "-")}[/]"),
                    new Markup($"[{typeHex}]{Markup.Escape(e.Type ?? "-")}[/]"),
                    new Markup($"[bold #{amtColor}]{Markup.Escape(FormatMoney(e.Total))}[/]"),
                    new Markup(originLabel)
                );
            }

            content = table;
        }

        return MakePanel(content, "Expense / Income list", focused);
    }

    // ── Generic panel factory ─────────────────────────────────────────────────
    private static Panel MakePanel(
        IRenderable content,
        string title,
        bool focused,
        Justify headerJustify = Justify.Left)
    {
        var borderColor = focused ? Theme.Warning : Theme.Border;
        var titleColor  = focused ? Theme.Warning : Theme.Muted;

        var panel = new Panel(content)
        {
            Border      = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Padding     = new Padding(1, 0),
            Expand      = true,
        };

        panel.Header = new PanelHeader(
            $"[{Hex(titleColor)}]{Markup.Escape(title)}[/]",
            headerJustify);

        return panel;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetDisplayName(string filePath)
    {
        var favs = ControlService.GetFavorites();
        var fav  = favs.FirstOrDefault(f =>
            string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        return fav?.Name ?? Path.GetFileNameWithoutExtension(filePath);
    }

    private static string FormatMoney(decimal value) => value.ToString("C2", Br);
}

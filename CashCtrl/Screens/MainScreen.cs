using CashCtrl.Models;
using CashCtrl.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace CashCtrl.Screens;

// ── Focus panels ─────────────────────────────────────────────────────────────
public enum MainFocus { None, Controls, Chart, Calendar, Total, Expenses, List }

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
        int listIdx     = 0;
        bool isDeleting = false;
        var  deleteKeys = new HashSet<string>();
        int calMonthIdx = -1; // -1 = auto (most recent month)

        while (true)
        {
            // Reload from disk to pick up saved entries
            var refreshed = await ControlService.LoadControlAsync(control.FilePath);
            if (refreshed is not null) control = refreshed;
            siblingControls = ControlService.FindControlsInDirectory(controlsDir);

            var period = control.Periods.Values.FirstOrDefault();
            var entries = period?.Entries
                .Select(kv => (key: kv.Key, entry: kv.Value))
                .OrderByDescending(x => x.entry.Date)
                .ToList() ?? new();

            if (listIdx >= entries.Count) listIdx = Math.Max(0, entries.Count - 1);

            DrawAll(control, siblingControls, controlsIdx, focus, listIdx, isDeleting, deleteKeys, calMonthIdx);

            var k = await Task.Run(() => Console.ReadKey(true));

            if (k.Key == ConsoleKey.Escape)
            {
                if (isDeleting) { isDeleting = false; deleteKeys.Clear(); continue; }
                if (focus == MainFocus.List) { focus = MainFocus.None; continue; }
                AnsiConsole.Clear(); return;
            }

            // Cycle calendar month
            if (k.KeyChar is 's' or 'S' && !isDeleting)
            {
                var p3 = control.Periods.Values.FirstOrDefault();
                var months = GetEntryMonths(p3);
                if (months.Count > 0)
                {
                    if (calMonthIdx < 0) calMonthIdx = months.Count - 1;
                    calMonthIdx = (calMonthIdx + 1) % months.Count;
                }
            }

            // Focus the Control list
            if (k.KeyChar is 'c' or 'C' && !isDeleting)
                focus = focus == MainFocus.Controls ? MainFocus.None : MainFocus.Controls;

            // Focus the entry list
            if (k.KeyChar is 'l' or 'L' && !isDeleting)
                focus = focus == MainFocus.List ? MainFocus.None : MainFocus.List;

            // Edit initial total balance
            if (k.KeyChar is 'i' or 'I' && !isDeleting)
            {
                DrawAll(control, siblingControls, controlsIdx, focus, listIdx, isDeleting, deleteKeys, calMonthIdx);
                var p2 = control.Periods.Values.FirstOrDefault();
                var currentTv = p2?.TotalValue ?? 0m;
                if (await EditTotalModal.ShowAsync(control, currentTv))
                {
                    var r = await ControlService.LoadControlAsync(control.FilePath);
                    if (r is not null) control = r;
                }
            }

            // Focus the Total money to create new income
            if (k.KeyChar is 't' or 'T' && !isDeleting)
            {
                focus = MainFocus.Total;
                DrawAll(control, siblingControls, controlsIdx, focus, listIdx, isDeleting, deleteKeys, calMonthIdx);
                var p2 = control.Periods.Values.FirstOrDefault();
                var currentTotal = (p2?.TotalValue ?? 0m) + (p2?.TotalIncome ?? 0m);
                if (await NewIncomeModal.ShowAsync(control, currentTotal))
                {
                    var r = await ControlService.LoadControlAsync(control.FilePath);
                    if (r is not null) control = r;
                }
            }

            // Focus the Total expense to create new expense
            if (k.KeyChar is 'e' or 'E' && !isDeleting)
            {
                focus = MainFocus.Expenses;
                DrawAll(control, siblingControls, controlsIdx, focus, listIdx, isDeleting, deleteKeys, calMonthIdx);
                if (await NewExpenseModal.ShowAsync(control))
                {
                    var r = await ControlService.LoadControlAsync(control.FilePath);
                    if (r is not null) control = r;
                }
            }

            if (focus == MainFocus.Controls && !isDeleting)
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

            if (focus == MainFocus.List)
            {
                if (k.Key == ConsoleKey.UpArrow   && listIdx > 0)                listIdx--;
                if (k.Key == ConsoleKey.DownArrow && listIdx < entries.Count - 1) listIdx++;

                // Enter delete mode with D
                if (k.KeyChar is 'd' or 'D' && !isDeleting && entries.Count > 0)
                {
                    isDeleting = true;
                    deleteKeys.Clear();
                    continue;
                }

                if (isDeleting)
                {
                    // Space toggles selection of current row
                    if (k.KeyChar == ' ' && entries.Count > 0)
                    {
                        var key = entries[listIdx].key;
                        if (!deleteKeys.Add(key)) deleteKeys.Remove(key);
                    }

                    // Enter confirms deletion
                    if (k.Key == ConsoleKey.Enter && deleteKeys.Count > 0)
                    {
                        var periodKey = ControlService.GetCurrentPeriodKey(control);
                        await ControlService.DeleteEntriesAsync(control, periodKey, deleteKeys);
                        var r = await ControlService.LoadControlAsync(control.FilePath);
                        if (r is not null) control = r;
                        isDeleting = false;
                        deleteKeys.Clear();
                        listIdx = 0;
                    }
                }
                else if (k.Key == ConsoleKey.Enter && entries.Count > 0)
                {
                    var selected = entries[listIdx].entry;
                    if (selected.Origin == "expense")
                        await ExpenseDetailModal.ShowAsync(selected);
                }
            }
        }
    }

    // ── Main draw — Spectre Layout (full-screen, responsive) ──────────────────
    private static void DrawAll(
        ControlFile control,
        List<string> siblings,
        int controlsIdx,
        MainFocus focus,
        int listIdx = 0,
        bool isDeleting = false,
        HashSet<string>? deleteKeys = null,
        int calMonthIdx = -1)
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
        var calPanel      = MakeCalendarPanel(period, focus == MainFocus.Calendar, calMonthIdx);

        // ── Build middle summary row (compact inline values + clock) ──────────
        var monLabel  = MakeSummaryPanel("Total Amount",    FormatMoney(totalMoney), Theme.Accent,           focus == MainFocus.Total);
        var expLabel  = MakeSummaryPanel("Total Expenses", FormatMoney(totalExp),   new Color(255, 80, 80), focus == MainFocus.Expenses);
        var diffLabel = MakeSummaryPanel("Available value",     FormatMoney(difference),
                                         difference >= 0 ? Theme.Accent : new Color(255, 80, 80), false);
        var clockLabel = MakeSummaryClockPanel();

        // ── Build bottom entry list ────────────────────────────────────────────
        var keyedEntries = period?.Entries
            .Select(kv => (key: kv.Key, entry: kv.Value))
            .OrderByDescending(x => x.entry.Date)
            .ToList() ?? new List<(string, ControlEntry)>();
        var listPanel = MakeEntryListPanel(keyedEntries, focus == MainFocus.List, listIdx, isDeleting, deleteKeys);

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
        int availableHeight = Math.Max(10, Console.WindowHeight - 1);
        // mid is fixed at 3 lines (top-border + value + bottom-border)
        const int midHeight  = 3;
        int remaining        = availableHeight - midHeight;
        // top gets 40% of remaining, list gets 60%
        int topHeight  = Math.Max(4, (int)(remaining * 0.40));
        int listHeight = Math.Max(4, remaining - topHeight);

        var layout = new Layout("root")
        {
            Size = availableHeight
        };
        layout.SplitRows(
            new Layout("top") { Size = topHeight }
                .SplitColumns(
                    new Layout("controls").Ratio(1),
                    new Layout("chart").Ratio(2),
                    new Layout("calendar").Ratio(1)
                ),
            new Layout("mid") { Size = midHeight }
                .SplitColumns(
                    new Layout("total").Ratio(1),
                    new Layout("exptotal").Ratio(1),
                    new Layout("diff").Ratio(1),
                    new Layout("clock").Ratio(1)
                ),
            new Layout("list") { Size = listHeight }
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

        // -> AVAILABLE COMMANDS
        var footer = $" [bold {Hex(Theme.Warning)}](C)[/][{Hex(Theme.Muted)}] controls   [/]" +
                     $"[bold {Hex(Theme.Warning)}](I)[/][{Hex(Theme.Muted)}] set balance   [/]" +
                     $"[bold {Hex(Theme.Warning)}](T)[/][{Hex(Theme.Muted)}] new income   [/]" +
                     $"[bold {Hex(Theme.Warning)}](E)[/][{Hex(Theme.Muted)}] new expense   [/]" +
                     $"[bold {Hex(Theme.Warning)}](L)[/][{Hex(Theme.Muted)}] list   [/]" +
                     $"[bold {Hex(Theme.Warning)}](S)[/][{Hex(Theme.Muted)}] calendar month   [/]" +
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

    private static List<(int year, int month)> GetEntryMonths(ControlPeriod? period)
    {
        var months = new SortedSet<(int year, int month)>();
        if (period is not null)
        {
            foreach (var e in period.Entries.Values)
            {
                if (DateTime.TryParseExact(e.Date, "dd/MM/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
                {
                    months.Add((dt.Year, dt.Month));
                }
            }
        }
        return months.ToList();
    }

    private static Panel MakeCalendarPanel(ControlPeriod? period, bool focused, int calMonthIdx = -1)
    {
        var months = GetEntryMonths(period);

        // Pick which month to display
        (int year, int month) displayMonth;
        if (months.Count == 0)
        {
            displayMonth = (DateTime.Now.Year, DateTime.Now.Month);
        }
        else
        {
            // Default (-1) = most recent = last in sorted list
            var idx = calMonthIdx < 0 || calMonthIdx >= months.Count
                ? months.Count - 1
                : calMonthIdx;
            displayMonth = months[idx];
        }

        // Build day map for the display month
        var dayMap = new Dictionary<int, (bool exp, bool inc)>();
        if (period is not null)
        {
            foreach (var e in period.Entries.Values)
            {
                if (DateTime.TryParseExact(e.Date, "dd/MM/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt)
                    && dt.Year == displayMonth.year && dt.Month == displayMonth.month)
                {
                    var cur = dayMap.GetValueOrDefault(dt.Day);
                    dayMap[dt.Day] = (cur.exp || e.Origin == "expense",
                                      cur.inc || e.Origin == "income");
                }
            }
        }

        var firstDay    = new DateTime(displayMonth.year, displayMonth.month, 1);
        int startDow    = (int)firstDay.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(displayMonth.year, displayMonth.month);

        var sb = new System.Text.StringBuilder();

        // Month tabs row (show each month as abbreviation, highlight active)
        if (months.Count > 1)
        {
            var activeIdx = calMonthIdx < 0 || calMonthIdx >= months.Count
                ? months.Count - 1
                : calMonthIdx;
            var tabSb = new System.Text.StringBuilder();
            for (int i = 0; i < months.Count; i++)
            {
                var m   = months[i];
                var lbl = new DateTime(m.year, m.month, 1).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture);
                if (i == activeIdx)
                    tabSb.Append($"[bold {Hex(Theme.Warning)}]{Markup.Escape(lbl)}[/]");
                else
                    tabSb.Append($"[{Hex(Theme.Muted)}]{Markup.Escape(lbl)}[/]");
                if (i < months.Count - 1) tabSb.Append($"[{Hex(Theme.Muted)}]|[/]");
            }
            sb.AppendLine(tabSb.ToString());
        }

        sb.AppendLine($"[bold {Hex(Theme.Secondary)}]{firstDay:MMMM yyyy}[/]");
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
        var time    = DateTime.Now.ToString("HH:mm");
        var content = Align.Center(
            new Markup($"[bold {Hex(Theme.Primary)}]{time}[/]"));
        return MakePanel(content, "Clock", false, Justify.Center);
    }

    private static Panel MakeEntryListPanel(
        List<(string key, ControlEntry entry)> entries,
        bool focused,
        int selectedIdx  = 0,
        bool isDeleting  = false,
        HashSet<string>? deleteKeys = null)
    {
        var dim = Hex(Theme.Muted);
        var sec = Hex(Theme.Secondary);
        var warn = Hex(Theme.Warning);
        const string red = "#FF6B6B";

        IRenderable content;
        if (entries.Count == 0)
        {
            content = new Markup($"[{dim}](no entries yet)[/]");
        }
        else
        {
            var table = new Table();
            table.Expand();
            table.Border(TableBorder.Rounded);
            table.BorderColor(focused || isDeleting ? Theme.Warning : Theme.Border);

            table.AddColumn(new TableColumn($"[{dim}]Date[/]"));
            table.AddColumn(new TableColumn($"[{dim}]Name[/]"));
            table.AddColumn(new TableColumn($"[{dim}]Type[/]"));
            table.AddColumn(new TableColumn($"[{dim}]Amount[/]").RightAligned());
            table.AddColumn(new TableColumn($"[{dim}]Origin[/]").Centered());

            for (int i = 0; i < entries.Count; i++)
            {
                var (key, e) = entries[i];
                var isIncome = e.Origin == "income";
                var isCursor = (focused || isDeleting) && i == selectedIdx;
                var isMarked = deleteKeys?.Contains(key) ?? false;

                // Use Description first; fall back to the JSON key (old format stored name as key)
                var displayName = !string.IsNullOrWhiteSpace(e.Description)
                    ? e.Description!
                    : key;
                // In delete mode: marked = red, cursor = yellow >prefix, else dim
                // In normal mode: cursor = yellow, else secondary
                string rowColor;
                if (isDeleting)
                    rowColor = isMarked ? red : (isCursor ? warn : dim);
                else
                    rowColor = isCursor ? warn : sec;

                var namePrefix = isCursor && isDeleting ? "> " : "  ";
                var dateStr    = Markup.Escape(e.Date);
                var nameStr    = Markup.Escape(namePrefix + displayName);
                var typeStr    = Markup.Escape(e.Type ?? "-");
                var amtStr     = Markup.Escape(FormatMoney(e.Total));

                // Type color: use entry TypeColor only in normal rows; red everything in delete mode
                var typeColor  = isDeleting || isCursor
                    ? rowColor
                    : (e.TypeColor is { Length: > 0 } tc ? $"#{tc}" : dim);

                var originStr = isIncome ? "income" : "expense";
                var originColor = isDeleting
                    ? rowColor
                    : (isIncome ? "#69DB7C" : red);

                table.AddRow(
                    new Markup($"[{rowColor}]{dateStr}[/]"),
                    new Markup($"[bold {rowColor}]{nameStr}[/]"),
                    new Markup($"[{typeColor}]{typeStr}[/]"),
                    new Markup($"[bold {rowColor}]{amtStr}[/]"),
                    new Markup($"[{originColor}]{Markup.Escape(originStr)}[/]")
                );
            }

            content = table;
        }

        // Build panel header directly so hint is raw markup (not escaped)
        var borderColor = focused || isDeleting ? Theme.Warning : Theme.Border;
        var titleColor  = focused || isDeleting ? Theme.Warning : Theme.Muted;
        var titleText   = $"[{Hex(titleColor)}]Expense / Income list[/]";
        string hintMarkup;
        if (isDeleting)
            hintMarkup = $"  [{dim}]Space: mark  Enter: delete  Esc: cancel[/]";
        else if (focused)
            hintMarkup = $"  [{dim}]navigate  Enter: detail  D: delete  Esc: exit[/]";
        else
            hintMarkup = "";

        var panel = new Panel(content)
        {
            Border      = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Padding     = new Padding(1, 0),
            Expand      = true,
        };
        panel.Header = new PanelHeader($"{titleText}{hintMarkup}", Justify.Left);
        return panel;
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

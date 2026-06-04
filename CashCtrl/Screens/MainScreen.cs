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
        int listIdx        = 0;
        int listScroll     = 0;  // top visible row index
        bool isDeleting    = false;
        var  deleteKeys    = new HashSet<string>();
        bool isControlsDeleting = false; // D key in Controls panel
        int calMonthIdx    = -1; // -1 = auto (most recent month)
        // Filter state: -1 = none, 0=Date, 1=Name, 2=Type, 3=Origin
        int     filterCol   = -1;
        string  filterText  = string.Empty; // applied filter (after Enter)
        string  filterInput = string.Empty; // live typing (not yet applied)
        bool    isFiltering = false;

        while (true)
        {
            // Reload from disk to pick up saved entries
            var refreshed = await ControlService.LoadControlAsync(control.FilePath);
            if (refreshed is not null) control = refreshed;
            siblingControls = ControlService.FindControlsInDirectory(controlsDir);

            var period = control.Periods.Values.FirstOrDefault();
            // Sort by parsed date descending (most recent first)
            var allEntries = period?.Entries
                .Select(kv => (key: kv.Key, entry: kv.Value))
                .OrderByDescending(x =>
                    DateTime.TryParseExact(x.entry.Date, "dd/MM/yyyy",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var d) ? d : DateTime.MinValue)
                .ToList() ?? new();

            // Apply column filter
            var entries = filterCol >= 0 && !string.IsNullOrEmpty(filterText)
                ? allEntries.Where(x =>
                {
                    var e = x.entry;
                    var haystack = (filterCol switch
                    {
                        0 => e.Date ?? "",
                        1 => (!string.IsNullOrWhiteSpace(e.Description) ? e.Description : x.key),
                        2 => e.Type ?? "",
                        3 => e.Origin ?? "",
                        _ => ""
                    }).Trim();
                    return haystack.Contains(filterText, StringComparison.OrdinalIgnoreCase);
                }).ToList()
                : allEntries;

            if (listIdx >= entries.Count) listIdx = Math.Max(0, entries.Count - 1);

            DrawAll(control, siblingControls, controlsIdx, focus, entries, listIdx, listScroll, isDeleting, deleteKeys, calMonthIdx, filterCol, filterText, isFiltering, isControlsDeleting, filterInput);

            var k = await Task.Run(() => Console.ReadKey(true));

            if (k.Key == ConsoleKey.Escape || (k.KeyChar is 'q' or 'Q' && !isFiltering))
            {
                if (isControlsDeleting) { isControlsDeleting = false; continue; }
                if (isDeleting) { isDeleting = false; deleteKeys.Clear(); continue; }
                if (focus != MainFocus.None) { focus = MainFocus.None; continue; }
                AnsiConsole.Clear(); return;
            }

            // Filter mode: F key enters filter mode
            if (k.KeyChar is 'f' or 'F' && !isDeleting && !isFiltering && focus == MainFocus.List)
            {
                isFiltering  = true;
                filterCol    = 0;
                filterText   = string.Empty;
                filterInput  = string.Empty;
                continue;
            }
            if (isFiltering)
            {
                if (k.Key == ConsoleKey.Escape)
                {
                    isFiltering  = false;
                    filterCol    = -1;
                    filterText   = string.Empty;
                    filterInput  = string.Empty;
                }
                else if (k.Key == ConsoleKey.Tab)
                {
                    filterCol   = (filterCol + 1) % 4;
                    filterInput = string.Empty; // clear pending input, keep applied filter until next Enter
                }
                else if (k.Key == ConsoleKey.Enter)
                {
                    // Apply pending input as the active filter
                    filterText  = filterInput;
                    isFiltering = false;
                    listIdx = 0; listScroll = 0;

                    // Check if the applied filter yields any results
                    if (!string.IsNullOrEmpty(filterText))
                    {
                        var period2 = control.Periods.Values.FirstOrDefault();
                        var allE2 = period2?.Entries
                            .Select(kv => (key: kv.Key, entry: kv.Value))
                            .ToList() ?? new();
                        bool hasMatch = allE2.Any(x =>
                        {
                            var e = x.entry;
                            var haystack = (filterCol switch
                            {
                                0 => e.Date ?? "",
                                1 => (!string.IsNullOrWhiteSpace(e.Description) ? e.Description : x.key),
                                2 => e.Type ?? "",
                                3 => e.Origin ?? "",
                                _ => ""
                            }).Trim();
                            return haystack.Contains(filterText, StringComparison.OrdinalIgnoreCase);
                        });
                        if (!hasMatch)
                        {
                            await ShowFilterNoResultsAsync(filterText);
                            filterText  = string.Empty;
                            filterCol   = -1;
                        }
                    }
                    continue;
                }
                else if (k.Key == ConsoleKey.Backspace)
                {
                    if (filterInput.Length > 0) filterInput = filterInput[..^1];
                }
                else if (k.KeyChar >= ' ')
                {
                    filterInput += k.KeyChar;
                }
                continue;
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
            if (k.KeyChar is 'c' or 'C' && !isDeleting && !isControlsDeleting)
            {
                focus = focus == MainFocus.Controls ? MainFocus.None : MainFocus.Controls;
                isControlsDeleting = false;
            }

            // Focus the entry list
            if (k.KeyChar is 'l' or 'L' && !isDeleting)
            {
                focus = focus == MainFocus.List ? MainFocus.None : MainFocus.List;
                listIdx = 0; listScroll = 0;
            }

            // B — toggle Total focus; when already focused open balance modal
            if (k.KeyChar is 'b' or 'B' && !isDeleting)
            {
                if (focus == MainFocus.Total)
                {
                    var p2 = control.Periods.Values.FirstOrDefault();
                    var currentTv = p2?.TotalValue ?? control.TotalAmount;
                    if (await EditTotalModal.ShowAsync(control, currentTv))
                    {
                        var r = await ControlService.LoadControlAsync(control.FilePath);
                        if (r is not null) control = r;
                    }
                }
                else
                {
                    focus = MainFocus.Total;
                }
            }

            // I — toggle Total focus; when already focused open income modal
            if (k.KeyChar is 'i' or 'I' && !isDeleting)
            {
                if (focus == MainFocus.Total)
                {
                    var p2 = control.Periods.Values.FirstOrDefault();
                    var currentTotal = (p2?.TotalValue ?? control.TotalAmount) + (p2?.TotalIncome ?? 0m);
                    if (await NewIncomeModal.ShowAsync(control, currentTotal))
                    {
                        var r = await ControlService.LoadControlAsync(control.FilePath);
                        if (r is not null) control = r;
                    }
                }
                else
                {
                    focus = MainFocus.Total;
                }
            }

            // E — transient modal, no persistent focus
            if (k.KeyChar is 'e' or 'E' && !isDeleting)
            {
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
                    if (!isControlsDeleting)
                    {
                        var nc = await ControlService.LoadControlAsync(siblingControls[controlsIdx]);
                        if (nc is not null)
                        {
                            control     = nc;
                            controlsDir = Path.GetDirectoryName(control.FilePath) ?? controlsDir;
                        }
                    }
                }

                // D: enter delete mode for controls
                if (k.KeyChar is 'd' or 'D' && !isControlsDeleting && siblingControls.Count > 0)
                {
                    isControlsDeleting = true;
                    continue;
                }

                // In controls delete mode: Enter = confirm delete
                if (isControlsDeleting && k.Key == ConsoleKey.Enter && siblingControls.Count > 0)
                {
                    var targetPath    = siblingControls[controlsIdx];
                    var targetName    = GetDisplayName(targetPath);
                    var targetControl = await ControlService.LoadControlAsync(targetPath) ?? control;
                    bool confirmed    = await ShowDeleteControlConfirmAsync(targetName, targetPath, targetControl);
                    if (confirmed)
                    {
                        await ControlService.DeleteControlAsync(targetPath);
                        siblingControls = ControlService.FindControlsInDirectory(controlsDir);
                        if (siblingControls.Count == 0)
                        {
                            AnsiConsole.Clear();
                            await WelcomeScreen.ShowAsync();
                            return;
                        }
                        // If deleted the active control, switch to the first available
                        if (string.Equals(targetPath, control.FilePath, StringComparison.OrdinalIgnoreCase))
                        {
                            var nc = await ControlService.LoadControlAsync(siblingControls[0]);
                            if (nc is not null) control = nc;
                        }
                        controlsIdx = Math.Clamp(controlsIdx, 0, siblingControls.Count - 1);
                    }
                    isControlsDeleting = false;
                    continue;
                }

                // N: create new control in the same directory
                if (k.KeyChar is 'n' or 'N' && !isControlsDeleting)
                {
                    DrawAll(control, siblingControls, controlsIdx, focus, entries, listIdx, listScroll, isDeleting, deleteKeys, calMonthIdx, filterCol, filterText, isFiltering, isControlsDeleting, filterInput);
                    var newPath = await NewControlModal.ShowAsync(controlsDir);
                    if (newPath is not null)
                    {
                        siblingControls = ControlService.FindControlsInDirectory(controlsDir);
                        var idx = siblingControls.IndexOf(newPath);
                        if (idx >= 0) controlsIdx = idx;
                        var nc = await ControlService.LoadControlAsync(newPath);
                        if (nc is not null) control = nc;
                    }
                }
            }

            if (focus == MainFocus.List)
            {
                // Mirror DrawAll visible-row calculation exactly
                int availH   = Math.Max(10, Console.WindowHeight - 1);
                int midH     = 3;
                int remaining2 = availH - midH;
                int topH     = Math.Max(4, (int)(remaining2 * 0.40));
                int listH    = Math.Max(4, remaining2 - topH);
                int rawRows  = Math.Max(2, listH - 4);
                int visRows  = Math.Max(1, rawRows / 2); // ShowRowSeparators doubles height

                if (k.Key == ConsoleKey.UpArrow && listIdx > 0)
                {
                    listIdx--;
                    if (listIdx < listScroll) listScroll = listIdx;
                }
                if (k.Key == ConsoleKey.DownArrow && listIdx < entries.Count - 1)
                {
                    listIdx++;
                    if (listIdx >= listScroll + visRows) listScroll = listIdx - visRows + 1;
                }

                // U: edit selected entry
                if (k.KeyChar is 'u' or 'U' && !isDeleting && entries.Count > 0)
                {
                    var (eKey, eEntry) = entries[listIdx];
                    var periodKey = ControlService.GetCurrentPeriodKey(control);
                    if (await EditEntryModal.ShowAsync(control, periodKey, eKey, eEntry))
                    {
                        var r = await ControlService.LoadControlAsync(control.FilePath);
                        if (r is not null) control = r;
                    }
                    continue;
                }

                // D: delete mode
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
        List<(string key, ControlEntry entry)> filteredEntries,
        int listIdx = 0,
        int listScroll = 0,
        bool isDeleting = false,
        HashSet<string>? deleteKeys = null,
        int calMonthIdx = -1,
        int filterCol = -1,
        string filterText = "",
        bool isFiltering = false,
        bool isControlsDeleting = false,
        string filterInput = "")
    {
        Console.CursorVisible = false;
        // Move to top-left instead of clearing to avoid flicker
        Console.SetCursorPosition(0, 0);

        var period = control.Periods.Values.FirstOrDefault();
        var totalMoney = (period?.TotalValue ?? control.TotalAmount) + (period?.TotalIncome ?? 0m);
        var totalExp   = period?.TotalExpenses ?? 0m;
        var difference = totalMoney - totalExp;

        // ── Build top panels ──────────────────────────────────────────────────
        var controlsPanel = MakeControlsPanel(siblings, controlsIdx, control.FilePath, focus == MainFocus.Controls, isControlsDeleting);
        var chartPanel    = MakeChartPanel(period, focus == MainFocus.Chart);
        var calPanel      = MakeCalendarPanel(period, focus == MainFocus.Calendar, calMonthIdx);

        // ── Build middle summary row (compact inline values + clock) ──────────
        var monLabel  = MakeSummaryPanel("Total Amount",    FormatMoney(totalMoney), Theme.Accent,           focus == MainFocus.Total);
        var expLabel  = MakeSummaryPanel("Total Expenses", FormatMoney(totalExp),   new Color(255, 80, 80), focus == MainFocus.Expenses);
        var diffLabel = MakeSummaryPanel("Available value",     FormatMoney(difference),
                                         difference >= 0 ? Theme.Accent : new Color(255, 80, 80), false);
        var clockLabel = MakeSummaryClockPanel();

        // ── Build bottom entry list ────────────────────────────────────────────
        var keyedEntries = filteredEntries;

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

        // Compute visible rows: ShowRowSeparators adds 1 line per row, so divide by 2
        int listPanelH  = Math.Max(4, listHeight);
        int rawRows     = Math.Max(2, listPanelH - 4); // subtract borders + header
        int visibleRows = Math.Max(1, rawRows / 2);    // each row = data + separator
        var listPanel   = MakeEntryListPanel(keyedEntries, focus == MainFocus.List, listIdx, listScroll, visibleRows, isDeleting, deleteKeys, filterCol, filterText, isFiltering, filterInput);

        var layout = new Layout("root")
        {
            Size = availableHeight
        };
        layout.SplitRows(
            new Layout("top") { Size = topHeight }
                .SplitColumns(
                    new Layout("controls").Ratio(2),
                    new Layout("chart").Ratio(3),
                    new Layout("calendar").Ratio(2)
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

        // -> UPDATE BANNER (shown when a newer version is detected)
        if (CashCtrl.Services.VersionService.IsOutdated)
        {
            AnsiConsole.MarkupLine(
                $" [bold yellow]\u26a0 New version available ({CashCtrl.Services.VersionService.LatestVersion}), run cash-ctrl --update to update[/]");
        }

        // -> DYNAMIC FOOTER per focus
        var fk  = Hex(Theme.Focus);
        var dim = Hex(Theme.Muted);
        string footer = focus switch
        {
            MainFocus.Controls when isControlsDeleting =>
                $" [{dim}]Controls: [/][bold {fk}]↑↓[/][{dim}] select   [/][bold {fk}]Enter[/][{dim}] confirm delete   [/][bold {fk}]Esc[/][{dim}] cancel[/]",
            MainFocus.Controls =>
                $" [{dim}]Controls: [/][bold {fk}]↑↓[/][{dim}] select   [/][bold {fk}]Enter[/][{dim}] open   [/][bold {fk}]D[/][{dim}] delete   [/][bold {fk}]N[/][{dim}] new   [/][bold {fk}]C[/][{dim}] exit panel   [/][bold {fk}]Esc[/][{dim}] quit[/]",
            MainFocus.List when isFiltering =>
                $" [{dim}]Filter: [/][bold {fk}]Tab[/][{dim}] next col   [/][bold {fk}]Enter[/][{dim}] confirm   [/][bold {fk}]Esc[/][{dim}] clear[/]",
            MainFocus.List when isDeleting =>
                $" [{dim}]Delete: [/][bold {fk}]↑↓[/][{dim}] select   [/][bold {fk}]Space[/][{dim}] mark   [/][bold {fk}]Enter[/][{dim}] delete   [/][bold {fk}]Esc[/][{dim}] cancel[/]",
            MainFocus.List =>
                $" [{dim}]List: [/][bold {fk}]↑↓[/][{dim}] scroll   [/][bold {fk}]Enter[/][{dim}] detail   [/][bold {fk}]D[/][{dim}] delete   [/][bold {fk}]U[/][{dim}] edit   [/][bold {fk}]F[/][{dim}] filter   [/][bold {fk}]L[/][{dim}] exit panel   [/][bold {fk}]Esc[/][{dim}] quit[/]",
            MainFocus.Total =>
                $" [{dim}]Total: [/][bold {fk}]I[/][{dim}] add income   [/][bold {fk}]B[/][{dim}] edit balance   [/][bold {fk}]Esc[/][{dim}] exit panel[/]",
            _ =>
                $" [{dim}]Panels: [/][bold {fk}]C[/][{dim}] controls   [/][bold {fk}]B[/][{dim}] balance   [/][bold {fk}]I[/][{dim}] income   [/][bold {fk}]E[/][{dim}] expense   [/][bold {fk}]L[/][{dim}] list   [/][bold {fk}]S[/][{dim}] cal month   [/][bold {fk}]Esc[/][{dim}] quit[/]"
        };
        // Pad to terminal width to erase any stale characters from a longer previous footer
        int termW2 = Math.Max(1, Console.WindowWidth);
        var footerPlain = System.Text.RegularExpressions.Regex.Replace(footer, @"\[.*?\]", "");
        int pad = Math.Max(0, termW2 - footerPlain.Length - 1);
        AnsiConsole.Markup(footer + new string(' ', pad));
    }

    // ── Panel builders ────────────────────────────────────────────────────────

    private static Panel MakeControlsPanel(
        List<string> siblings, int selectedIdx, string currentPath, bool focused, bool isControlsDeleting = false)
    {
        var red  = "#FF6B6B";
        var rows = new List<string>();
        foreach (var (fp, i) in siblings.Select((f, i) => (f, i)))
        {
            var name      = GetDisplayName(fp);
            var isCurrent = string.Equals(fp, currentPath, StringComparison.OrdinalIgnoreCase);
            var isSel     = i == selectedIdx;
            string line;
            if (isControlsDeleting && isSel)
                line = $"[bold {red}]> {Markup.Escape(name)}[/]";
            else if (isCurrent && isSel)
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

        var borderColor = isControlsDeleting ? Color.Red : (focused ? Theme.Focus : Theme.Border);
        var titleColor  = isControlsDeleting ? Color.Red : (focused ? Theme.Focus : Theme.Muted);

        var panel = new Panel(content)
        {
            Border      = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Padding     = new Padding(1, 0),
            Expand      = true,
        };
        panel.Header = new PanelHeader($"[{Hex(titleColor)}]Controls[/]", Justify.Left);
        return panel;
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

        int termW = Math.Max(20, Console.WindowWidth);
        bool compactChart = termW < 120;

        IRenderable content;
        if (byType.Count == 0)
        {
            content = new Markup($"[{Hex(Theme.Muted)}](no expenses yet)[/]");
        }
        else
        {
            var types  = byType.OrderByDescending(kv => kv.Value.total).ToList();
            var sb     = new System.Text.StringBuilder();

            if (compactChart)
            {
                // Compact: type name + total value only, no bars
                int maxLabel = Math.Min(types.Max(kv => kv.Key.Length), 12);
                foreach (var (typeName, (val, colHex)) in types)
                {
                    var label  = typeName.Length > maxLabel ? typeName[..maxLabel] : typeName.PadRight(maxLabel);
                    var valStr = FormatMoney(val);
                    sb.AppendLine(
                        $"[#{colHex}]{Markup.Escape(label)}[/] " +
                        $"[{Hex(Theme.Muted)}]{Markup.Escape(valStr)}[/]");
                }
            }
            else
            {
                var maxVal   = byType.Values.Max(v => v.total);
                int maxLabel = Math.Min(types.Max(kv => kv.Key.Length), 14);
                int barMax   = Math.Max(1, 40);
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
        int scrollOffset = 0,
        int visibleRows  = 20,
        bool isDeleting  = false,
        HashSet<string>? deleteKeys = null,
        int filterCol   = -1,
        string filterText = "",
        bool isFiltering = false,
        string filterInput = "")
    {
        var dim  = Hex(Theme.Muted);
        var sec  = Hex(Theme.Secondary);
        var warn = Hex(Theme.Focus);    // neutral purple cursor highlight
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
            table.Border(TableBorder.Simple);
            table.ShowRowSeparators();
            // Row separators always dim — outer focus color comes from the wrapping Panel
            table.BorderColor(Theme.Border);

            // Column headers — highlight the active filter column
            string ColHdr(int col, string label)
            {
                if (isFiltering && filterCol == col)
                    return $"[bold {Hex(Theme.Focus)}]{label}[/] [bold {Hex(Theme.Warning)}]▶ {Markup.Escape(filterInput)}|[/]";
                if (!isFiltering && filterCol == col && !string.IsNullOrEmpty(filterText))
                    return $"[bold {Hex(Theme.Focus)}]{label}[/] [{Hex(Theme.Muted)}]({Markup.Escape(filterText)})[/]";
                return $"[{dim}]{label}[/]";
            }

            table.AddColumn(new TableColumn(ColHdr(0, "Date")));
            table.AddColumn(new TableColumn(ColHdr(1, "Name")));
            table.AddColumn(new TableColumn(ColHdr(2, "Type")));
            table.AddColumn(new TableColumn($"[{dim}]Amount[/]").RightAligned());
            table.AddColumn(new TableColumn(ColHdr(3, "Origin")).Centered());

            // Clamp scroll offset
            int maxScroll = Math.Max(0, entries.Count - visibleRows);
            scrollOffset  = Math.Clamp(scrollOffset, 0, maxScroll);
            int end = Math.Min(entries.Count, scrollOffset + visibleRows);

            for (int i = scrollOffset; i < end; i++)
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
        var borderColor = focused || isDeleting ? Theme.Focus : Theme.Border;
        var titleColor  = focused || isDeleting ? Theme.Focus : Theme.Muted;
        var titleText   = $"[{Hex(titleColor)}]Expense / Income list[/]";

        var panel = new Panel(content)
        {
            Border      = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Padding     = new Padding(1, 0),
            Expand      = true,
        };
        panel.Header = new PanelHeader(titleText, Justify.Left);
        return panel;
    }

    // ── Generic panel factory ─────────────────────────────────────────────────
    private static Panel MakePanel(
        IRenderable content,
        string title,
        bool focused,
        Justify headerJustify = Justify.Left)
    {
        var borderColor = focused ? Theme.Focus : Theme.Border;
        var titleColor  = focused ? Theme.Focus : Theme.Muted;

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

    // ── Filter no-results modal ───────────────────────────────────────────────

    private static async Task ShowFilterNoResultsAsync(string term)
    {
        int w  = Math.Max(Console.WindowWidth  > 0 ? Console.WindowWidth  : 80, 40);
        int h  = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 8);
        int mw = Math.Min(64, w - 4);
        int mx = (w - mw) / 2;
        int inner = mw - 2;
        var brd = "#6E64A0";
        var dim = Hex(Theme.Muted);
        var red = "#FF6B6B";

        string Row(string content)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(content, @"\[.*?\]", "");
            var pad   = Math.Max(0, inner - plain.Length);
            return $"[{brd}]│[/]{content}{new string(' ', pad)}[{brd}]│[/]";
        }

        var termDisp = term.Length > inner - 20 ? term[..(inner - 20)] : term;
        var msg      = $"No results for \"{termDisp}\" into the list";
        var msgPad   = Math.Max(0, (inner - msg.Length) / 2);
        var hint     = "Press any key to continue...";
        var hintPad  = 2;

        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', inner)}╮[/]",
            Row(""),
            Row($"{new string(' ', msgPad)}[bold {red}]{Markup.Escape(msg)}[/]"),
            Row(""),
            Row($"{new string(' ', hintPad)}[{dim}]{Markup.Escape(hint)}[/]"),
            Row(""),
            $"[{brd}]╰{new string('─', inner)}╯[/]",
        };

        int mh = lines.Count;
        int my = Math.Max(0, (h - mh) / 2);

        // Overlay on top of the existing screen — no Clear
        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
        }

        await Task.Run(() => Console.ReadKey(true));
    }

    // ── Delete-control confirmation modal ────────────────────────────────────

    private static async Task<bool> ShowDeleteControlConfirmAsync(
        string controlName, string filePath, ControlFile current)
    {
        var period = current.Periods.Values.FirstOrDefault();
        var total  = period?.TotalValue ?? current.TotalAmount;
        var brPt   = new System.Globalization.CultureInfo("pt-BR");

        int w  = Math.Max(Console.WindowWidth  > 0 ? Console.WindowWidth  : 80, 50);
        int h  = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 12);
        int mw = Math.Min(72, w - 4);
        int mx = (w - mw) / 2;
        int mh = 11;
        int my = Math.Max(0, (h - mh) / 2);

        var brd  = "#6E64A0";
        var fk   = Hex(Theme.Focus);
        var dim  = Hex(Theme.Muted);
        var sec  = Hex(Theme.Secondary);
        var acc  = Hex(Theme.Accent);
        var red  = "#FF6B6B";
        var inner = mw - 2;

        string Row(string content)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(content, @"\[.*?\]", "");
            var pad   = Math.Max(0, inner - plain.Length);
            return $"[{brd}]│[/]{content}{new string(' ', pad)}[{brd}]│[/]";
        }
        string Sep(string l = "├", string r = "┤") =>
            $"[{brd}]{l}{new string('─', inner)}{r}[/]";

        // Truncate file path from left if too long
        var displayPath = filePath;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (displayPath.StartsWith(home, StringComparison.OrdinalIgnoreCase))
            displayPath = "~" + displayPath[home.Length..];
        int pathBudget = inner - 8; // "  File : " prefix
        if (displayPath.Length > pathBudget && pathBudget > 3)
            displayPath = "…" + displayPath[^(pathBudget - 1)..];

        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', inner)}╮[/]",
            Row($"  [{dim}]{Markup.Escape(controlName)}[/]"),
            Sep(),
            Row($"  [{dim}]Name : [/][bold {sec}]{Markup.Escape(controlName)}[/]"),
            Row($"  [{dim}]Value: [/][bold {acc}]{Markup.Escape(total.ToString("C2", brPt))}[/]"),
            Row($"  [{dim}]File : [/][{sec}]{Markup.Escape(displayPath)}[/]"),
            Sep(),
            Row($"  [bold {red}]Delete \"{Markup.Escape(controlName)}\"? This cannot be undone.[/]"),
            Sep(),
            Row($"  [bold {fk}]Enter[/] [{dim}]confirm delete   [/]  [bold {fk}]any other key[/] [{dim}]cancel[/]"),
            $"[{brd}]╰{new string('─', inner)}╯[/]",
        };

        AnsiConsole.Clear();
        for (int i = 0; i < mh && i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
            var plain = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "");
            Console.Write(new string(' ', Math.Max(0, mw - plain.Length)));
        }

        var key = await Task.Run(() => Console.ReadKey(true));
        return key.Key == ConsoleKey.Enter;
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

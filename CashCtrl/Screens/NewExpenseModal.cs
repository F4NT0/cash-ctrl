using CashCtrl.Models;
using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class NewExpenseModal
{
    private static readonly System.Globalization.CultureInfo Br = new("pt-BR");

    // Predefined palette for type colors (cycles if user picks existing type)
    private static readonly string[] Palette =
    {
        "FF6B6B", "FFA94D", "FFD43B", "69DB7C", "4DABF7",
        "CC5DE8", "F783AC", "63E6BE", "74C0FC", "A9E34B",
    };

    private static int _paletteIdx = 0;

    public static async Task<bool> ShowAsync(ControlFile control)
    {
        var periodKey = ControlService.GetCurrentPeriodKey(control);
        var period    = control.Periods.GetValueOrDefault(periodKey);

        // Collect existing types → colors mapping
        var typeColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (period is not null)
        {
            foreach (var e in period.Entries.Values.Where(e => e.Origin == "expense" && e.Type is not null))
                typeColors.TryAdd(e.Type!, e.TypeColor ?? Palette[0]);
        }

        string name      = string.Empty;
        decimal amount   = 0m;
        string type      = string.Empty;
        string typeColor = string.Empty;
        string date      = DateTime.Now.ToString("dd/MM/yyyy");
        // item tuple: name, quantity, size (Kg/Un), itemPrice (Kg), amount (Un)
        var items        = new List<(string name, int qty, string unit, decimal itemPrice, decimal amount)>();
        int field        = 0; // 0=name, 1=amount, 2=type, 3=date
        bool inItems     = false;
        // itemField: 0=iname, 1=iqty, 2=iunit, 3=iprice/iamount
        int  itemField   = 0;

        // Temporary item being built
        string  iName      = string.Empty;
        int     iQty       = 1;
        string  iUnit      = string.Empty;  // "Kg" or "Un"
        decimal iItemPrice = 0m;            // used when Kg
        decimal iAmount    = 0m;            // used when Un

        while (true)
        {
            DrawModal(name, amount, type, typeColor, date, items,
                      field, inItems, itemField,
                      iName, iQty, iUnit, iItemPrice, iAmount, typeColors);

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Escape)
            {
                if (inItems) { inItems = false; continue; }
                return false;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                if (inItems)
                {
                    // Commit current item if non-empty name
                    if (!string.IsNullOrWhiteSpace(iName))
                    {
                        var isKg      = string.Equals(iUnit, "Kg", StringComparison.OrdinalIgnoreCase);
                        var finalAmt  = isKg ? iItemPrice * iQty : iAmount;
                        items.Add((iName, iQty, iUnit, isKg ? iItemPrice : 0m, finalAmt));
                        iName = string.Empty; iQty = 1; iUnit = string.Empty;
                        iItemPrice = 0m; iAmount = 0m; itemField = 0;
                    }
                    inItems = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name) || amount <= 0) { field = 0; continue; }

                // Resolve color
                if (string.IsNullOrWhiteSpace(type)) type = "Other";
                if (!typeColors.TryGetValue(type, out var color))
                {
                    color = Palette[_paletteIdx % Palette.Length];
                    _paletteIdx++;
                    typeColors[type] = color;
                }
                typeColor = color;

                var entry = new ControlEntry
                {
                    Date        = string.IsNullOrWhiteSpace(date) ? DateTime.Now.ToString("dd/MM/yyyy") : date,
                    Total       = amount,
                    Type        = type,
                    TypeColor   = typeColor,
                    Description = name,
                    Origin      = "expense",
                    Details     = items.Select(i => new EntryItem
                    {
                        Name      = i.name,
                        ItemPrice = i.itemPrice,
                        Size      = i.unit,
                        Quantity  = i.qty,
                        Amount    = i.amount
                    }).ToList()
                };

                await ControlService.SaveExpenseAsync(control, periodKey, entry);

                var reloaded = await ControlService.LoadControlAsync(control.FilePath);
                if (reloaded is not null) control.Periods = reloaded.Periods;

                return true;
            }

            if (key.Key == ConsoleKey.Tab && !inItems)
            {
                field = (field + 1) % 4;
                continue;
            }

            // '+' opens item entry
            if (key.KeyChar == '+' && !inItems)
            {
                inItems = true; itemField = 0;
                continue;
            }

            // Item sub-field navigation
            if (inItems && key.Key == ConsoleKey.Tab)
            {
                // Fields: 0=name, 1=qty, 2=size, 3=price(Kg)/amount(Un)
                // Only show field 3 if size is set
                var maxField = string.IsNullOrEmpty(iUnit) ? 2 : 3;
                itemField = (itemField + 1) % (maxField + 1);
                continue;
            }

            // Text input routing
            if (key.Key == ConsoleKey.Backspace)
            {
                if (inItems)
                {
                    if (itemField == 0 && iName.Length > 0)
                        iName = iName[..^1];
                    else if (itemField == 1 && iQty > 1)
                        iQty = int.Parse(iQty.ToString()[..^1].Length > 0 ? iQty.ToString()[..^1] : "1");
                    else if (itemField == 2 && iUnit.Length > 0)
                    {
                        iUnit = iUnit[..^1];
                        iItemPrice = 0m; iAmount = 0m;
                    }
                    else if (itemField == 3)
                    {
                        var isKg = string.Equals(iUnit, "Kg", StringComparison.OrdinalIgnoreCase);
                        if (isKg && iItemPrice > 0)
                        {
                            var s = iItemPrice.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "");
                            s = s.Length > 1 ? s[..^1] : "0";
                            iItemPrice = decimal.Parse(s) / 100m;
                        }
                        else if (!isKg && iAmount > 0)
                        {
                            var s = iAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "");
                            s = s.Length > 1 ? s[..^1] : "0";
                            iAmount = decimal.Parse(s) / 100m;
                        }
                    }
                }
                else
                {
                    if (field == 0 && name.Length > 0) name = name[..^1];
                    else if (field == 1 && amount > 0)
                    {
                        var s = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "");
                        s = s.Length > 1 ? s[..^1] : "0";
                        amount = decimal.Parse(s) / 100m;
                    }
                    else if (field == 2 && type.Length > 0) type = type[..^1];
                    else if (field == 3 && date.Length > 0) date = date[..^1];
                }
            }
            else if (key.KeyChar >= ' ')
            {
                if (inItems)
                {
                    if (itemField == 0)
                        iName += key.KeyChar;
                    else if (itemField == 1 && char.IsDigit(key.KeyChar))
                    {
                        var s = iQty.ToString() + key.KeyChar;
                        if (int.TryParse(s, out var q)) iQty = q;
                    }
                    else if (itemField == 2)
                    {
                        iUnit += key.KeyChar;
                        iItemPrice = 0m; iAmount = 0m;
                    }
                    else if (itemField == 3)
                    {
                        var isKg = string.Equals(iUnit, "Kg", StringComparison.OrdinalIgnoreCase);
                        if (char.IsDigit(key.KeyChar))
                        {
                            if (isKg)
                            {
                                var s = iItemPrice.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "") + key.KeyChar;
                                iItemPrice = decimal.Parse(s) / 100m;
                            }
                            else
                            {
                                var s = iAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "") + key.KeyChar;
                                iAmount = decimal.Parse(s) / 100m;
                            }
                        }
                    }
                }
                else
                {
                    if (field == 0) name += key.KeyChar;
                    else if (field == 1 && char.IsDigit(key.KeyChar))
                    {
                        var s = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", "") + key.KeyChar;
                        amount = decimal.Parse(s) / 100m;
                    }
                    else if (field == 2) type += key.KeyChar;
                    else if (field == 3) date += key.KeyChar;
                }
            }
        }
    }

    private static void DrawModal(
        string name, decimal amount, string type, string typeColor, string date,
        List<(string name, int qty, string unit, decimal itemPrice, decimal amount)> items,
        int field, bool inItems, int itemField,
        string iName, int iQty, string iUnit, decimal iItemPrice, decimal iAmount,
        Dictionary<string, string> typeColors)
    {
        int w  = Math.Max(Console.WindowWidth  > 0 ? Console.WindowWidth  : 80, 50);
        int h  = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 12);
        int mw = Math.Min(68, w - 4);

        var lines = BuildLines(name, amount, type, typeColor, date, items,
                               field, inItems, itemField,
                               iName, iQty, iUnit, iItemPrice, iAmount, typeColors, mw);

        int mh = lines.Count;
        int mx = (w - mw) / 2;
        int my = Math.Max(0, (h - mh) / 2);

        Console.CursorVisible = false;
        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
            var plain = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\[.*?\]", "");
            Console.Write(new string(' ', Math.Max(0, mw - plain.Length)));
        }
    }

    private static List<string> BuildLines(
        string name, decimal amount, string type, string typeColor, string date,
        List<(string name, int qty, string unit, decimal itemPrice, decimal amount)> items,
        int field, bool inItems, int itemField,
        string iName, int iQty, string iUnit, decimal iItemPrice, decimal iAmount,
        Dictionary<string, string> typeColors, int mw)
    {
        var p    = $"#{Theme.Primary.R:X2}{Theme.Primary.G:X2}{Theme.Primary.B:X2}";
        var foc  = $"#{Theme.Focus.R:X2}{Theme.Focus.G:X2}{Theme.Focus.B:X2}";
        var sec  = $"#{Theme.Secondary.R:X2}{Theme.Secondary.G:X2}{Theme.Secondary.B:X2}";
        var dim  = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var acc  = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";
        var warn = $"#{Theme.Warning.R:X2}{Theme.Warning.G:X2}{Theme.Warning.B:X2}";
        var brd  = "#6E64A0";
        int inner = mw - 2;

        // Active field = bold focus color + cursor block; inactive = secondary
        string Hl(bool active, string s) => active
            ? $"[bold {foc}]{Markup.Escape(s.Length > 0 ? s : "")}[/][bold {warn}]|[/]"
            : $"[{sec}]{Markup.Escape(s.Length > 0 ? s : "_")}[/]";

        string Row(string content)
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(content, @"\[.*?\]", "");
            var pad   = Math.Max(0, inner - plain.Length);
            return $"[{brd}]│[/]{content}{new string(' ', pad)}[{brd}]│[/]";
        }

        string Sep(string left = "├", string right = "┤") =>
            $"[{brd}]{left}{new string('─', inner)}{right}[/]";

        var brPt   = new System.Globalization.CultureInfo("pt-BR");
        var amtStr = amount.ToString("C2", brPt);

        var dispColor = string.IsNullOrEmpty(typeColor)
            ? (typeColors.TryGetValue(type, out var c) ? c : "AAAAAA")
            : typeColor;
        var typeDisplay = string.IsNullOrEmpty(type)
            ? $"[{dim}]_[/]"
            : $"[bold #{dispColor}]{Markup.Escape(type)}[/]";

        // ── Header fields ─────────────────────────────────────────────────────
        var lines = new List<string>
        {
            $"[{brd}]╭{new string('─', inner)}╮[/]",
            Row($"  [{dim}]New Expense[/]"),
            Sep(),
            Row($"  [{dim}]Name  [/] {Hl(field == 0 && !inItems, string.IsNullOrEmpty(name) ? "" : name)}"),
            Row($"  [{dim}]Amount[/] {Hl(field == 1 && !inItems, amtStr)}"),
            Row($"  [{dim}]Type  [/] {(field == 2 && !inItems ? $"[bold {foc}]{Markup.Escape(string.IsNullOrEmpty(type) ? "" : type)}[/][bold {warn}]|[/]" : typeDisplay)}"),
            Row($"  [{dim}]Date  [/] {Hl(field == 3 && !inItems, string.IsNullOrEmpty(date) ? "" : date)}"),
        };

        // ── Items section ─────────────────────────────────────────────────────
        lines.Add(Sep());
        lines.Add(Row($"  [{dim}]{"Name",-18} {"Qty",3}  {"Size",-4} {"Value",10}[/]"));
        lines.Add(Sep());

        foreach (var item in items)
        {
            var isKg   = string.Equals(item.unit, "Kg", StringComparison.OrdinalIgnoreCase);
            var valStr = isKg
                ? $"{item.itemPrice.ToString("C2", brPt)}/Kg={item.amount.ToString("C2", brPt)}"
                : item.amount.ToString("C2", brPt);
            var nm = item.name.Length > 18 ? item.name[..18] : item.name.PadRight(18);
            lines.Add(Row(
                $"  [{sec}]{Markup.Escape(nm)}[/]" +
                $" [{dim}]{item.qty,3}  {Markup.Escape(item.unit.PadRight(4))}[/]" +
                $" [{acc}]{Markup.Escape(valStr),10}[/]"));
        }

        // ── Active item entry ─────────────────────────────────────────────────
        if (inItems)
        {
            var isKg = string.Equals(iUnit, "Kg", StringComparison.OrdinalIgnoreCase);
            string priceField;
            if (string.IsNullOrEmpty(iUnit))
                priceField = "";
            else if (isKg)
            {
                var calc = iItemPrice * iQty;
                priceField = $" [{dim}]→[/] [{acc}]{Markup.Escape(calc.ToString("C2", brPt))}[/]";
            }
            else
                priceField = $" {Hl(itemField == 3, iAmount.ToString("C2", brPt))}";

            lines.Add(Row(
                $"  {Hl(itemField == 0, string.IsNullOrEmpty(iName) ? "" : iName)}" +
                $" [{dim}]qty[/]{Hl(itemField == 1, iQty.ToString())}" +
                $" [{dim}]sz[/]{Hl(itemField == 2, string.IsNullOrEmpty(iUnit) ? "" : iUnit)}" +
                (!string.IsNullOrEmpty(iUnit) ? $" [{dim}]val[/]{(isKg ? Hl(itemField == 3, iItemPrice.ToString("C2", brPt)) : Hl(itemField == 3, iAmount.ToString("C2", brPt)))}" : "") +
                (isKg && !string.IsNullOrEmpty(iUnit) ? $" [{acc}]={Markup.Escape((iItemPrice*iQty).ToString("C2",brPt))}[/]" : "")));
        }
        else
        {
            lines.Add(Row($"  [{dim}]+ add item[/]"));
        }

        // ── Footer hint ───────────────────────────────────────────────────────
        lines.Add(Sep("├", "┤"));
        var hint = inItems
            ? $"  [{dim}]Tab next  Enter confirm  Esc back[/]"
            : $"  [{dim}]+item  Tab field  Enter save  Esc cancel[/]";
        lines.Add(Row(hint));
        lines.Add($"[{brd}]╰{new string('─', inner)}╯[/]");

        return lines;
    }
}

using System.Globalization;
using System.Text.RegularExpressions;
using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class CalculatorModal
{
    private static readonly CultureInfo Br = new("pt-BR");
    private static readonly List<(string Expr, string Result)> History = new();

    public static void Show()
    {
        var expr = string.Empty;
        string? error = null;
        string? last = null;

        while (true)
        {
            Draw(expr, error);

            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Escape) return;

            error = null;
            if (k.Key == ConsoleKey.Enter)
            {
                if (string.IsNullOrWhiteSpace(expr)) continue;
                try
                {
                    var r = Evaluate(expr);
                    last = Format(r);
                    History.Add((expr, last));
                    expr = last.Replace(".", "");
                }
                catch (Exception ex) { error = ex.Message; }
            }
            else if (k.Key == ConsoleKey.Backspace) { if (expr.Length > 0) expr = expr[..^1]; }
            else if (k.Key == ConsoleKey.Delete) expr = string.Empty;
            else if (k.KeyChar is >= '0' and <= '9' or '.' or ',' or '(' or ')')
                expr += k.KeyChar;
            else if (k.KeyChar is '+' or '-' or '*' or '/' or '^' or '%')
            {
                if (expr.Length == 0 && last is not null && k.KeyChar != '-') expr = last.Replace(".", "");
                expr += k.KeyChar;
            }
            else if (k.KeyChar is 'x' or 'X') expr += '*';
        }
    }

    private static string Format(double v) => v.ToString("#,##0.########", Br);

    private static void Draw(string expr, string? error)
    {
        int w = Math.Max(Console.WindowWidth > 0 ? Console.WindowWidth : 80, 40);
        int h = Math.Max(Console.WindowHeight > 0 ? Console.WindowHeight : 24, 12);
        int mw = Math.Min(42, w - 4);
        int inner = mw - 2;
        int mx = Math.Max(0, w - mw - 2);
        int my = 2;
        int histRows = Math.Clamp(h - 16, 3, 12);

        var brd = "#6E64A0";
        var dim = $"#{Theme.Muted.R:X2}{Theme.Muted.G:X2}{Theme.Muted.B:X2}";
        var acc = $"#{Theme.Accent.R:X2}{Theme.Accent.G:X2}{Theme.Accent.B:X2}";

        string Row(string plain, string style)
        {
            if (plain.Length > inner - 2) plain = plain[^(inner - 2)..];
            return $"[{brd}]│[/] [{style}]{Markup.Escape(plain)}[/]{new string(' ', inner - 1 - plain.Length)}[{brd}]│[/]";
        }
        string Pair(string l, string r, string ls, string rs)
        {
            var maxL = inner - 4 - r.Length;
            if (l.Length > maxL) l = l[^Math.Max(0, maxL)..];
            return $"[{brd}]│[/] [{ls}]{Markup.Escape(l)}[/]{new string(' ', Math.Max(0, inner - 2 - l.Length - r.Length))}[{rs}]{Markup.Escape(r)}[/] [{brd}]│[/]";
        }

        var title = " Calculator ";
        var lines = new List<string>
        {
            $"[{brd}]╭─[/][bold {acc}]{title}[/][{brd}]{new string('─', inner - 1 - title.Length)}╮[/]",
            Row("History (this session)", dim),
        };

        var shown = History.Skip(Math.Max(0, History.Count - histRows)).ToList();
        for (int i = 0; i < histRows; i++)
        {
            var idx = i - (histRows - shown.Count);
            lines.Add(idx >= 0
                ? Pair(shown[idx].Expr, "= " + shown[idx].Result, "white", $"bold {acc}")
                : Row("", dim));
        }

        lines.Add($"[{brd}]├{new string('─', inner)}┤[/]");
        lines.Add(error is not null ? Row(error, "red") : Row("", dim));
        lines.Add(Row(expr + "_", "bold white"));
        lines.Add($"[{brd}]├{new string('─', inner)}┤[/]");
        lines.Add(Row("Enter calc  Del clear  Esc close", dim));
        lines.Add(Row("+ - * / ^ % ( )   x = multiply", dim));
        lines.Add($"[{brd}]╰{new string('─', inner)}╯[/]");

        for (int i = 0; i < lines.Count; i++)
        {
            try { Console.SetCursorPosition(mx, my + i); } catch { }
            AnsiConsole.Markup(lines[i]);
        }
    }

    // ── Expression evaluator (recursive descent) ─────────────────────────────

    private static double Evaluate(string input)
    {
        var s = Regex.Replace(input, @"\s+", "");
        // pt-BR: '.' thousands separator is not typed; accept ',' or '.' as decimal
        s = s.Replace(',', '.');
        int pos = 0;

        double ParseExpr()
        {
            var v = ParseTerm();
            while (pos < s.Length && s[pos] is '+' or '-')
            {
                var op = s[pos++];
                var r = ParseTerm();
                v = op == '+' ? v + r : v - r;
            }
            return v;
        }
        double ParseTerm()
        {
            var v = ParsePow();
            while (pos < s.Length && s[pos] is '*' or '/' or '%')
            {
                var op = s[pos++];
                var r = ParsePow();
                if (op != '*' && r == 0) throw new DivideByZeroException("Division by zero");
                v = op == '*' ? v * r : op == '/' ? v / r : v % r;
            }
            return v;
        }
        double ParsePow()
        {
            var b = ParseUnary();
            if (pos < s.Length && s[pos] == '^') { pos++; return Math.Pow(b, ParsePow()); }
            return b;
        }
        double ParseUnary()
        {
            if (pos < s.Length && s[pos] == '-') { pos++; return -ParseUnary(); }
            if (pos < s.Length && s[pos] == '+') { pos++; return ParseUnary(); }
            return ParsePrimary();
        }
        double ParsePrimary()
        {
            if (pos < s.Length && s[pos] == '(')
            {
                pos++;
                var v = ParseExpr();
                if (pos >= s.Length || s[pos] != ')') throw new FormatException("Missing ')'");
                pos++;
                return v;
            }
            int start = pos;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.')) pos++;
            if (start == pos || !double.TryParse(s[start..pos], NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                throw new FormatException("Invalid expression");
            return n;
        }

        var result = ParseExpr();
        if (pos != s.Length) throw new FormatException("Invalid expression");
        if (double.IsNaN(result) || double.IsInfinity(result)) throw new ArithmeticException("Invalid result");
        return result;
    }
}

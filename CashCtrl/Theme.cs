using Spectre.Console;
using Spectre.Console.Rendering;

namespace CashCtrl;

public static class Theme
{
    // ── Colors ──────────────────────────────────────────────────────────────
    public static readonly Color Primary   = new(157, 136, 255);  // lavender purple
    public static readonly Color Secondary = new(220, 215, 255);  // light purple
    public static readonly Color Accent    = new(80,  220, 140);  // mint green (money)
    public static readonly Color Info      = new(80,  195, 255);  // sky blue (paths)
    public static readonly Color Muted     = new(100, 100, 115);  // dim gray
    public static readonly Color Border    = new(55,  50,  80);   // dark purple border
    public static readonly Color Warning   = new(255, 200, 80);   // amber
    public static readonly Color Focus     = new(180, 160, 255);  // neutral light purple (selected)

    // ── Styles ──────────────────────────────────────────────────────────────
    public static Style PrimaryStyle   => new(Primary);
    public static Style SecondaryStyle => new(Secondary);
    public static Style AccentStyle    => new(Accent);
    public static Style InfoStyle      => new(Info);
    public static Style MutedStyle     => new(Muted);
    public static Style BoldPrimary    => new(Primary, decoration: Decoration.Bold);

    // ── Markup helpers ───────────────────────────────────────────────────────
    public static string Colored(string text, Color color)   => $"[#{color.R:X2}{color.G:X2}{color.B:X2}]{Markup.Escape(text)}[/]";
    public static string Purple(string text)                  => Colored(text, Primary);
    public static string Green(string text)                   => Colored(text, Accent);
    public static string Cyan(string text)                    => Colored(text, Info);
    public static string Gray(string text)                    => Colored(text, Muted);
    public static string Light(string text)                   => Colored(text, Secondary);
    public static string Bold(string text, Color? c = null)   => $"[bold {(c.HasValue ? $"#{c.Value.R:X2}{c.Value.G:X2}{c.Value.B:X2}" : "white")}]{Markup.Escape(text)}[/]";

    // ── Panel builders ───────────────────────────────────────────────────────
    public static Panel MakePanel(IRenderable content, string title = "")
    {
        var panel = new Panel(content)
        {
            Border      = BoxBorder.Rounded,
            BorderStyle = new Style(Border),
            Padding     = new Padding(2, 1)
        };

        if (!string.IsNullOrEmpty(title))
            panel.Header = new PanelHeader($" {title} ", Justify.Left);

        return panel;
    }

    public static Panel MakePanelBright(IRenderable content, string title = "")
    {
        var brightBorder = new Color(110, 100, 160); // lighter purple border
        var panel = new Panel(content)
        {
            Border      = BoxBorder.Rounded,
            BorderStyle = new Style(brightBorder),
            Padding     = new Padding(2, 1)
        };

        if (!string.IsNullOrEmpty(title))
            panel.Header = new PanelHeader(
                $"[bold #{Secondary.R:X2}{Secondary.G:X2}{Secondary.B:X2}]{Markup.Escape(title)}[/]",
                Justify.Left);

        return panel;
    }

    public static Rule MakeDivider(string? text = null)
    {
        var rule = text is null
            ? new Rule()
            : new Rule($"[#{Muted.R:X2}{Muted.G:X2}{Muted.B:X2}]{Markup.Escape(text)}[/]");

        rule.Style = new Style(Border);
        return rule;
    }
}

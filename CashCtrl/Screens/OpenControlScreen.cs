using CashCtrl.Models;
using CashCtrl.Services;
using Spectre.Console;

namespace CashCtrl.Screens;

public static class OpenControlScreen
{
    public static async Task ShowAsync(string filePath)
    {
        AnsiConsole.Clear();

        var control = await ControlService.LoadControlAsync(filePath);

        if (control is null)
        {
            AnsiConsole.MarkupLine($"[red]Could not open file:[/] {Markup.Escape(filePath)}");
            Console.ReadKey(true);
            return;
        }

        await ShowControlPreviewAsync(control);
    }

    public static async Task ShowControlPreviewAsync(ControlFile control)
    {
        AnsiConsole.Clear();

        var period   = control.Periods.Values.FirstOrDefault();
        var total    = period?.TotalValue ?? 0m;
        var currency = new System.Globalization.CultureInfo("pt-BR");

        var rows = new List<string>
        {
            $"  {Theme.Gray("Control name:")}   {Theme.Light(control.Name)}",
            $"  {Theme.Gray("Total value:")}    {Theme.Green(total.ToString("C2", currency))}",
            $"  {Theme.Gray("File:")}           {Theme.Cyan(control.FilePath)}",
        };

        var content = new Markup(string.Join("\n", rows) + "\n\n" +
                                 Theme.Gray("─────────────────────────────────────────────────────────────────") + "\n" +
                                 $"  {Theme.Gray("Enter:")} {Theme.Light("open control")}   {Theme.Gray("|")}   {Theme.Gray("Esc:")} {Theme.Light("exit")}");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(
            new Padder(
                Theme.MakePanel(content, $" {control.Name} ")
            ).PadLeft(2).PadRight(2)
        );
        AnsiConsole.WriteLine();

        while (true)
        {
            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                await ControlService.AddToFavoritesAsync(control.FilePath, control.Name);
                MainScreen.Show(control);
                return;
            }

            if (key.Key == ConsoleKey.Escape) return;
        }
    }
}

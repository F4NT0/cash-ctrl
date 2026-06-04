using System.Text.Json;
using CashCtrl.Models;

namespace CashCtrl.Services;

public static class ControlService
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CashCtrl");

    private static readonly string FavoritesPath =
        Path.Combine(AppDataDir, "favorites.json");

    public static async Task CreateControlAsync(string filePath, string controlName, decimal totalValue)
    {
        var data = new Dictionary<string, object>
        {
            ["name"]         = controlName,
            ["total-amount"] = totalValue
        };

        var json = JsonSerializer.Serialize(data, JsonOptions.Default);
        await File.WriteAllTextAsync(filePath, json);

        await AddToFavoritesAsync(filePath, controlName);
    }

    public static async Task<ControlFile?> LoadControlAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        var json = await File.ReadAllTextAsync(filePath);

        // Parse via JsonElement so we can skip scalar top-level keys
        // (new format has "name" and "total-amount" alongside period objects)
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? embeddedName   = null;
        decimal embeddedAmount = 0m;
        var periods = new Dictionary<string, ControlPeriod>();

        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    var period = prop.Value.Deserialize<ControlPeriod>(JsonOptions.Default);
                    if (period is not null)
                        periods[prop.Name] = period;
                }
                catch { }
            }
            else if (prop.Name == "name" && prop.Value.ValueKind == JsonValueKind.String)
            {
                embeddedName = prop.Value.GetString();
            }
            else if (prop.Name == "total-amount" && prop.Value.ValueKind == JsonValueKind.Number)
            {
                embeddedAmount = prop.Value.GetDecimal();
            }
        }

        // Prefer embedded name, then favorites, then file slug
        var favName = GetFavorites()
            .FirstOrDefault(f => string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            ?.Name;

        var name = favName ?? embeddedName ?? Path.GetFileNameWithoutExtension(filePath);

        var control = new ControlFile
        {
            Name        = name,
            FilePath    = filePath,
            TotalAmount = embeddedAmount,
            Periods     = periods
        };

        return control;
    }

    public static List<FavoriteEntry> GetFavorites()
    {
        if (!File.Exists(FavoritesPath)) return new List<FavoriteEntry>();

        try
        {
            var json = File.ReadAllText(FavoritesPath);
            return JsonSerializer.Deserialize<List<FavoriteEntry>>(json, JsonOptions.Default)
                   ?? new List<FavoriteEntry>();
        }
        catch
        {
            return new List<FavoriteEntry>();
        }
    }

    public static async Task AddToFavoritesAsync(string filePath, string name)
    {
        Directory.CreateDirectory(AppDataDir);

        var favorites = GetFavorites();
        favorites.RemoveAll(f => string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        favorites.Insert(0, new FavoriteEntry
        {
            Name = name,
            FilePath = filePath,
            LastOpened = DateTime.Now
        });

        if (favorites.Count > 20)
            favorites = favorites.Take(20).ToList();

        var json = JsonSerializer.Serialize(favorites, JsonOptions.Default);
        await File.WriteAllTextAsync(FavoritesPath, json);
    }

    public static async Task RemoveFromFavoritesAsync(string filePath)
    {
        var favorites = GetFavorites();
        favorites.RemoveAll(f => string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        var json = JsonSerializer.Serialize(favorites, JsonOptions.Default);
        await File.WriteAllTextAsync(FavoritesPath, json);
    }

    public static async Task SaveExpenseAsync(ControlFile control, string periodKey, ControlEntry expense)
    {
        var period = control.Periods.GetValueOrDefault(periodKey)
                     ?? new ControlPeriod { TotalValue = 0 };

        var entryKey = $"expense-{DateTime.Now:yyyyMMddHHmmss}";

        // Re-serialize the whole period with the new entry injected via ExtensionData
        var periodDict = BuildPeriodDict(period);
        periodDict[entryKey] = expense;

        control.Periods[periodKey] = period;
        await SaveControlAsync(control, periodDict, periodKey);
    }

    public static async Task DeleteControlAsync(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);

        // Remove from favorites
        var favorites = GetFavorites();
        favorites.RemoveAll(f => string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        var json = JsonSerializer.Serialize(favorites, JsonOptions.Default);
        await File.WriteAllTextAsync(FavoritesPath, json);
    }

    public static async Task DeleteEntriesAsync(ControlFile control, string periodKey, IEnumerable<string> entryKeys)
    {
        var period = control.Periods.GetValueOrDefault(periodKey);
        if (period is null) return;

        var keysToRemove = new HashSet<string>(entryKeys);
        var periodDict   = BuildPeriodDict(period);

        foreach (var k in keysToRemove)
            periodDict.Remove(k);

        // Also remove from ExtensionData so reloaded in-memory totals are correct
        if (period.ExtensionData is not null)
            foreach (var k in keysToRemove)
                period.ExtensionData.Remove(k);

        await SaveControlAsync(control, periodDict, periodKey);
    }

    public static async Task UpdateEntryAsync(
        ControlFile control, string periodKey, string entryKey, ControlEntry updated)
    {
        var period = control.Periods.GetValueOrDefault(periodKey);
        if (period is null) return;

        var periodDict = BuildPeriodDict(period);
        periodDict[entryKey] = updated;

        control.Periods[periodKey] = period;
        await SaveControlAsync(control, periodDict, periodKey);
    }

    public static async Task SaveIncomeAsync(ControlFile control, string periodKey, ControlEntry income)
    {
        var period = control.Periods.GetValueOrDefault(periodKey)
                     ?? new ControlPeriod { TotalValue = 0 };

        var entryKey = $"income-{DateTime.Now:yyyyMMddHHmmss}";

        var periodDict = BuildPeriodDict(period);
        periodDict[entryKey] = income;

        control.Periods[periodKey] = period;
        await SaveControlAsync(control, periodDict, periodKey);
    }

    public static async Task SaveTotalValueAsync(ControlFile control, string periodKey, decimal newTotalValue)
    {
        // New format: persist total-amount at top level
        control.TotalAmount = newTotalValue;

        var period = control.Periods.GetValueOrDefault(periodKey)
                     ?? new ControlPeriod { TotalValue = 0 };

        var periodDict = BuildPeriodDict(period);
        periodDict["total-value"] = newTotalValue;

        control.Periods[periodKey] = period;
        await SaveControlAsync(control, periodDict, periodKey);
    }

    private static Dictionary<string, object> BuildPeriodDict(ControlPeriod period)
    {
        var dict = new Dictionary<string, object>
        {
            ["total-value"] = period.TotalValue
        };

        foreach (var (k, entry) in period.Entries)
            dict[k] = entry;

        return dict;
    }

    private static async Task SaveControlAsync(
        ControlFile control,
        Dictionary<string, object> updatedPeriodDict,
        string updatedPeriodKey)
    {
        // Always start with name + total-amount at top level
        var fileDict = new Dictionary<string, object>
        {
            ["name"]         = control.Name,
            ["total-amount"] = control.TotalAmount
        };

        // Append all periods (replace the updated one)
        foreach (var (key, period) in control.Periods)
        {
            fileDict[key] = key == updatedPeriodKey
                ? updatedPeriodDict
                : BuildPeriodDict(period);
        }

        // Also include the updated period if it wasn't in control.Periods yet
        if (!control.Periods.ContainsKey(updatedPeriodKey))
            fileDict[updatedPeriodKey] = updatedPeriodDict;

        var json = JsonSerializer.Serialize(fileDict, JsonOptions.Default);
        await File.WriteAllTextAsync(control.FilePath, json);
    }

    public static string GetCurrentPeriodKey(ControlFile control)
    {
        var now    = DateTime.Now;
        var months = new[]
        {
            "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
            "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
        };
        var key = $"{months[now.Month - 1]} {now.Year}";
        return control.Periods.ContainsKey(key) ? key : (control.Periods.Keys.FirstOrDefault() ?? key);
    }

    public static List<string> FindControlsInDirectory(string directory)
    {
        try
        {
            return Directory.GetFiles(directory, "*.json")
                .Where(f => IsControlFile(f))
                .OrderByDescending(File.GetLastWriteTime)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static bool IsControlFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return false;

            // New format: top-level "total-amount" key
            if (root.TryGetProperty("total-amount", out _))
                return true;

            // Legacy format: period object with "total-value" key
            foreach (var period in root.EnumerateObject())
            {
                if (period.Value.ValueKind == JsonValueKind.Object &&
                    period.Value.TryGetProperty("total-value", out _))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public static string GetCurrentPeriodKey()
    {
        var now = DateTime.Now;
        var months = new[]
        {
            "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
            "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
        };
        return $"{months[now.Month - 1]} {now.Year}";
    }

    private static string GetCurrentPeriodName() => GetCurrentPeriodKey();
}

public class FavoriteEntry
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime LastOpened { get; set; }
}

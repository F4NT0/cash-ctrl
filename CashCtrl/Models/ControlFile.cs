using System.Text.Json;
using System.Text.Json.Serialization;

namespace CashCtrl.Models;

public class ControlFile
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    public Dictionary<string, ControlPeriod> Periods { get; set; } = new();
}

public class ControlPeriod
{
    [JsonPropertyName("total-value")]
    public decimal TotalValue { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    [JsonIgnore]
    public Dictionary<string, ControlEntry> Entries
    {
        get
        {
            var result = new Dictionary<string, ControlEntry>();
            if (ExtensionData is null) return result;

            foreach (var (key, element) in ExtensionData)
            {
                try
                {
                    var entry = element.Deserialize<ControlEntry>(JsonOptions.Default);
                    if (entry is not null)
                        result[key] = entry;
                }
                catch
                {
                    // skip non-entry keys
                }
            }

            return result;
        }
    }

    [JsonIgnore]
    public decimal TotalExpenses =>
        Entries.Values
            .Where(e => e.Origin == "expense")
            .Sum(e => e.Total);

    [JsonIgnore]
    public decimal TotalIncome =>
        Entries.Values
            .Where(e => e.Origin == "income")
            .Sum(e => e.Total);

    [JsonIgnore]
    public decimal Balance => TotalValue + TotalIncome - TotalExpenses;
}

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}

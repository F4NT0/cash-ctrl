using System.Text.Json.Serialization;

namespace CashCtrl.Models;

public class ControlEntry
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("type-color")]
    public string? TypeColor { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("origin")]
    public string Origin { get; set; } = "expense";

    [JsonPropertyName("details")]
    public List<EntryItem> Details { get; set; } = new();
}

public class EntryItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("item-price")]
    public decimal ItemPrice { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("size")]
    public string Size { get; set; } = string.Empty;
}

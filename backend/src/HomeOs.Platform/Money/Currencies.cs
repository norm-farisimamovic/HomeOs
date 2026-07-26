namespace HomeOs.Platform.Money;

/// <summary>A supported currency: ISO-ish code, display symbol, name, and its rate to the base (BAM).</summary>
public sealed record CurrencyInfo(string Code, string Symbol, string Name, decimal RateToBase);

/// <summary>
/// Static currency registry + conversion. Base is <c>BAM</c> (Bosnian mark, shown as "KM"). Rates are the
/// number of BAM in one unit of the currency; EUR is the fixed peg. Rates are intentionally static (no
/// external FX call — CSP-safe and deterministic); a future app could refresh them behind this same API.
/// </summary>
public static class Currencies
{
    public const string Base = "BAM";

    private static readonly IReadOnlyDictionary<string, CurrencyInfo> Map =
        new Dictionary<string, CurrencyInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["BAM"] = new("BAM", "KM", "Konvertibilna marka", 1m),
            ["EUR"] = new("EUR", "€", "Euro", 1.95583m),
            ["USD"] = new("USD", "$", "US dollar", 1.80m),
            ["GBP"] = new("GBP", "£", "British pound", 2.28m),
            ["CHF"] = new("CHF", "CHF", "Swiss franc", 2.05m),
            ["RSD"] = new("RSD", "дин", "Serbian dinar", 0.0167m),
        };

    /// <summary>All supported currencies (for the picker).</summary>
    public static IReadOnlyCollection<CurrencyInfo> All => (IReadOnlyCollection<CurrencyInfo>)Map.Values;

    /// <summary>Legacy "KM" maps to BAM; unknown codes fall back to the base.</summary>
    public static string Normalize(string? code) =>
        string.IsNullOrWhiteSpace(code) ? Base
        : string.Equals(code, "KM", StringComparison.OrdinalIgnoreCase) ? Base
        : Map.ContainsKey(code) ? Map[code].Code : Base;

    public static CurrencyInfo Get(string? code) => Map[Normalize(code)];

    /// <summary>Converts <paramref name="amount"/> from one currency to another (2-dp rounded).</summary>
    public static decimal Convert(decimal amount, string? from, string? to)
    {
        var fromRate = Get(from).RateToBase;
        var toRate = Get(to).RateToBase;
        return Math.Round(amount * fromRate / toRate, 2);
    }
}

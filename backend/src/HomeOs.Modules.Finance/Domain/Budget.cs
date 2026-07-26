namespace HomeOs.Modules.Finance.Domain;

/// <summary>A household's monthly spending limit for one expense category.</summary>
public sealed class Budget
{
    private Budget() { }

    /// <summary>Creates a budget for a category with a monthly limit in the given currency.</summary>
    public static Budget Create(Guid householdId, string category, decimal monthlyLimit, string currency) => new()
    {
        HouseholdId = householdId,
        Category = category.Trim(),
        MonthlyLimit = Math.Round(Math.Abs(monthlyLimit), 2),
        Currency = string.IsNullOrWhiteSpace(currency) ? "BAM" : currency.Trim(),
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid HouseholdId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public decimal MonthlyLimit { get; private set; }
    public string Currency { get; private set; } = "BAM";
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Updates the limit (and the currency it's expressed in).</summary>
    public void SetLimit(decimal monthlyLimit, string currency)
    {
        MonthlyLimit = Math.Round(Math.Abs(monthlyLimit), 2);
        Currency = string.IsNullOrWhiteSpace(currency) ? Currency : currency.Trim();
    }
}

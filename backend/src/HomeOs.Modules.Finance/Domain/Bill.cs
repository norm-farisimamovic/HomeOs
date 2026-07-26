using HomeOs.Platform.Entities;

namespace HomeOs.Modules.Finance.Domain;

/// <summary>How often a bill recurs.</summary>
public enum BillCadence { Monthly = 0, Quarterly = 1, Yearly = 2, OneOff = 3 }

/// <summary>A recurring bill or subscription with a next-due date.</summary>
public sealed class Bill : IHomeObject
{
    private Bill() { }

    public static Bill Create(Guid householdId, Guid ownerId, string name, decimal amount, string currency,
        BillCadence cadence, DateOnly nextDue, string category, Guid? whoPaysId, Visibility visibility) => new()
    {
        HouseholdId = householdId,
        OwnerId = ownerId,
        Name = name.Trim(),
        Amount = Math.Round(Math.Abs(amount), 2),
        Currency = string.IsNullOrWhiteSpace(currency) ? "KM" : currency.Trim(),
        Cadence = cadence,
        NextDue = nextDue,
        Category = category.Trim(),
        WhoPaysId = whoPaysId,
        Visibility = visibility,
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ObjectType => "bill";
    public Guid HouseholdId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "KM";
    public BillCadence Cadence { get; private set; }
    public DateOnly NextDue { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public Guid? WhoPaysId { get; private set; }
    public Visibility Visibility { get; private set; } = Visibility.Household;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>When the most recent "due soon" alert was sent (null = never).</summary>
    public DateTimeOffset? NotifiedAtUtc { get; private set; }

    /// <summary>
    /// The lead-day stage last alerted at (days before due; 0 = due-day). Null = no alert yet. Drives the
    /// escalating "in 7 / 3 / 1 days, then today" ladder so each stage fires once (see <c>LeadSchedule</c>).
    /// </summary>
    public int? NotifiedLeadDays { get; private set; }

    public bool IsDueWithin(DateOnly today, int days) => NextDue >= today && NextDue <= today.AddDays(days);

    /// <summary>Whether this bill repeats (anything other than a one-off).</summary>
    public bool IsRecurring => Cadence != BillCadence.OneOff;

    /// <summary>Records that the alert for a given lead-day stage has been sent.</summary>
    public void MarkNotified(int stage)
    {
        NotifiedLeadDays = stage;
        NotifiedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Advances a recurring bill to its next occurrence at or after <paramref name="today"/> (so a long gap
    /// skips missed cycles rather than firing a burst) and resets the alert ladder for the new cycle. No-op
    /// for one-off bills.
    /// </summary>
    public void RollForwardTo(DateOnly today)
    {
        if (!IsRecurring) return;
        var next = NextDue;
        for (var guard = 0; next < today && guard < 600; guard++)
            next = Advance(next, Cadence);
        NextDue = next;
        NotifiedLeadDays = null;
        NotifiedAtUtc = null;
    }

    private static DateOnly Advance(DateOnly date, BillCadence cadence) => cadence switch
    {
        BillCadence.Monthly => date.AddMonths(1),
        BillCadence.Quarterly => date.AddMonths(3),
        BillCadence.Yearly => date.AddYears(1),
        _ => date,
    };
}

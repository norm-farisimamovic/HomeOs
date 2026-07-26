using HomeOs.Platform.Entities;

namespace HomeOs.Modules.Finance.Domain;

/// <summary>Money direction.</summary>
public enum TransactionKind { Expense = 0, Income = 1 }

/// <summary>A single expense or income entry, attributed to the member who paid/received.</summary>
public sealed class Transaction : IHomeObject
{
    private Transaction() { }

    public static Transaction Create(Guid householdId, Guid ownerId, TransactionKind kind, decimal amount,
        string currency, string category, DateOnly occurredOn, string? description, Guid paidById, Visibility visibility) => new()
    {
        HouseholdId = householdId,
        OwnerId = ownerId,
        Kind = kind,
        Amount = Math.Round(Math.Abs(amount), 2),
        Currency = string.IsNullOrWhiteSpace(currency) ? "KM" : currency.Trim(),
        Category = category.Trim(),
        OccurredOn = occurredOn,
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        PaidById = paidById,
        Visibility = visibility,
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ObjectType => "transaction";
    public Guid HouseholdId { get; private set; }
    public Guid OwnerId { get; private set; }
    public TransactionKind Kind { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "KM";
    public string Category { get; private set; } = string.Empty;
    public DateOnly OccurredOn { get; private set; }
    public string? Description { get; private set; }
    public Guid PaidById { get; private set; }
    public Visibility Visibility { get; private set; } = Visibility.Household;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
}

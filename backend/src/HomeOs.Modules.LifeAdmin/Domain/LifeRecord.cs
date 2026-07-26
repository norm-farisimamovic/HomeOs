using HomeOs.Platform.Entities;

namespace HomeOs.Modules.LifeAdmin.Domain;

/// <summary>Kind of life-admin record.</summary>
public enum LifeCategory { Document = 0, Warranty = 1, Insurance = 2, Subscription = 3, Contact = 4, Other = 5 }

/// <summary>
/// A household life-admin record — a document, warranty, insurance policy, subscription or contact,
/// optionally with an expiry/renewal date that drives an automatic reminder.
/// </summary>
public sealed class LifeRecord : IHomeObject
{
    private LifeRecord() { }

    public static LifeRecord Create(Guid householdId, Guid ownerId, string title, LifeCategory category,
        DateOnly? expiresOn, string? provider, string? notes, Visibility visibility) => new()
    {
        HouseholdId = householdId,
        OwnerId = ownerId,
        Title = title.Trim(),
        Category = category,
        ExpiresOn = expiresOn,
        Provider = provider?.Trim(),
        Notes = notes?.Trim(),
        Visibility = visibility,
    };

    /// <summary>Edits the mutable fields in place.</summary>
    public void Update(string title, LifeCategory category, DateOnly? expiresOn, string? provider, string? notes, Visibility visibility)
    {
        Title = title.Trim();
        Category = category;
        ExpiresOn = expiresOn;
        Provider = provider?.Trim();
        Notes = notes?.Trim();
        Visibility = visibility;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ObjectType => "liferecord";
    public Guid HouseholdId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public LifeCategory Category { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public string? Provider { get; private set; }
    public string? Notes { get; private set; }
    public Visibility Visibility { get; private set; } = Visibility.Household;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
}

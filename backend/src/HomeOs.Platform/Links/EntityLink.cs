namespace HomeOs.Platform.Links;

/// <summary>
/// A link from one app object to another — the kernel's "connected web". The target's type/id plus a
/// denormalized title + deep-link are stored so any app can show its links without resolving the other
/// module (which it may not reference, or which may not be installed).
/// </summary>
public sealed class EntityLink
{
    private EntityLink() { }

    /// <summary>Creates a link from (fromType, fromId) to a target object described by a title + deep link.</summary>
    public static EntityLink Create(Guid householdId, string fromType, Guid fromId,
        string toType, Guid toId, string toTitle, string toLink) => new()
    {
        HouseholdId = householdId,
        FromType = fromType,
        FromId = fromId,
        ToType = toType,
        ToId = toId,
        ToTitle = toTitle,
        ToLink = toLink,
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid HouseholdId { get; private set; }
    public string FromType { get; private set; } = string.Empty;
    public Guid FromId { get; private set; }
    public string ToType { get; private set; } = string.Empty;
    public Guid ToId { get; private set; }
    public string ToTitle { get; private set; } = string.Empty;
    public string ToLink { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
}

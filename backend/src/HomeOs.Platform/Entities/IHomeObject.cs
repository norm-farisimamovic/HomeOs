namespace HomeOs.Platform.Entities;

/// <summary>
/// Every linkable domain object across the platform. `HouseholdId` is the hard tenancy boundary;
/// `ObjectType` identifies the kind for the connected web (entity links).
/// </summary>
public interface IHomeObject
{
    /// <summary>Primary key.</summary>
    Guid Id { get; }

    /// <summary>Stable kind discriminator, e.g. "task", "bill", "note", "event".</summary>
    string ObjectType { get; }

    /// <summary>Owning household (tenancy boundary).</summary>
    Guid HouseholdId { get; }
}

/// <summary>Who can see or change an item.</summary>
public enum Visibility
{
    /// <summary>Owner only.</summary>
    Private = 0,

    /// <summary>Everyone in the household.</summary>
    Household = 1,

    /// <summary>Specific members (share list); owner + assignee always included.</summary>
    Shared = 2,
}

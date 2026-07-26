namespace HomeOs.Platform.Audit;

/// <summary>An immutable record of something notable that happened in a household — for the owner/admin log.</summary>
public sealed class AuditEntry
{
    private AuditEntry() { }

    public static AuditEntry Create(Guid householdId, Guid? actorMemberId, string action, string detail) => new()
    {
        HouseholdId = householdId,
        ActorMemberId = actorMemberId,
        Action = action,
        Detail = detail,
    };

    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Owning household (tenancy boundary).</summary>
    public Guid HouseholdId { get; private set; }

    /// <summary>Who did it (null for system actions).</summary>
    public Guid? ActorMemberId { get; private set; }

    /// <summary>Dotted action key, e.g. <c>member.invited</c>, <c>task.completed</c>.</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>Human detail (name/title of the affected thing).</summary>
    public string Detail { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
}

namespace HomeOs.Platform.Scoreboard;

/// <summary>
/// One awarded points record, tied to the thing that earned it (e.g. a completed task). The unique
/// (household, source) key makes awards idempotent — completing the same task twice never double-counts,
/// and un-completing it can revoke the exact entry.
/// </summary>
public sealed class PointsEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid MemberId { get; set; }
    public int Points { get; set; }

    /// <summary>What earned the points (e.g. <c>task</c>).</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>The id of the earning entity (e.g. the task id).</summary>
    public Guid SourceId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

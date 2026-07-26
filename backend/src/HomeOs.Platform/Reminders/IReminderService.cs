namespace HomeOs.Platform.Reminders;

/// <summary>
/// A reminder any app asks the platform to schedule on behalf of one of its objects (a bill's due date,
/// a warranty's expiry…). Tied to a source (<c>SourceKey</c> = owning app e.g. <c>lifeadmin</c>, <c>SourceId</c>
/// = the object's id) so it can be idempotently updated/removed when that object changes — this is how
/// "everything connects" without apps referencing the Reminders module directly.
/// </summary>
public sealed record ScheduledReminder(
    Guid HouseholdId,
    Guid OwnerId,
    Guid ForMemberId,
    string Title,
    DateOnly RemindOn,
    TimeOnly? RemindAt,
    string SourceKey,
    Guid SourceId);

/// <summary>Kernel capability: schedule/remove reminders. Implemented by the Reminders module.</summary>
public interface IReminderService
{
    /// <summary>Creates — or updates, if one already exists for the same source — a reminder.</summary>
    Task ScheduleAsync(ScheduledReminder reminder, CancellationToken cancellationToken = default);

    /// <summary>Removes any reminder tied to the given source object.</summary>
    Task RemoveAsync(Guid householdId, string sourceKey, Guid sourceId, CancellationToken cancellationToken = default);
}

using HomeOs.Modules.Reminders.Domain;
using HomeOs.Platform.Reminders;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Reminders.Persistence;

/// <summary>
/// The Reminders module's implementation of the kernel <see cref="IReminderService"/>. Any app can
/// schedule reminders through this without referencing Reminders — they just depend on the interface.
/// Idempotent per source: scheduling twice for the same source updates the existing reminder.
/// </summary>
public sealed class ReminderService(RemindersDbContext db) : IReminderService
{
    /// <inheritdoc />
    public async Task ScheduleAsync(ScheduledReminder r, CancellationToken cancellationToken = default)
    {
        var existing = await db.Reminders.FirstOrDefaultAsync(
            x => x.HouseholdId == r.HouseholdId && x.SourceKey == r.SourceKey && x.SourceId == r.SourceId,
            cancellationToken);

        if (existing is null)
        {
            db.Reminders.Add(Reminder.CreateForSource(
                r.HouseholdId, r.OwnerId, r.ForMemberId, r.Title, r.RemindOn, r.RemindAt, r.SourceKey, r.SourceId));
        }
        else
        {
            existing.Reschedule(r.Title, r.RemindOn, r.RemindAt);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Guid householdId, string sourceKey, Guid sourceId, CancellationToken cancellationToken = default)
    {
        var linked = await db.Reminders
            .Where(x => x.HouseholdId == householdId && x.SourceKey == sourceKey && x.SourceId == sourceId)
            .ToListAsync(cancellationToken);
        if (linked.Count == 0) return;
        db.Reminders.RemoveRange(linked);
        await db.SaveChangesAsync(cancellationToken);
    }
}

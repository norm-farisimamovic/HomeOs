using HomeOs.Modules.Reminders.Persistence;
using HomeOs.Platform.Digest;
using HomeOs.Platform.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Reminders.Digest;

/// <summary>Contributes a member's upcoming, not-yet-done reminders to their digest.</summary>
public sealed class RemindersUpcomingProvider(RemindersDbContext db) : IUpcomingProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<UpcomingItem>> GetUpcomingAsync(
        Guid householdId, Guid memberId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var reminders = await db.Reminders.AsNoTracking()
            .Where(r => r.HouseholdId == householdId && !r.IsDone
                && r.RemindOn >= from && r.RemindOn <= to
                && (r.OwnerId == memberId || r.ForMemberId == memberId || r.Visibility == Visibility.Household))
            .ToListAsync(ct);

        return reminders.Select(r => new UpcomingItem("reminders", r.Title, r.RemindOn, "reminder")).ToList();
    }
}

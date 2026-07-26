using HomeOs.Modules.Reminders.Persistence;
using HomeOs.Platform.Calendar;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Reminders.Calendar;

/// <summary>Contributes reminders to the shared calendar (respecting the member's visibility).</summary>
public sealed class RemindersCalendarSource(ICurrentMember me, RemindersDbContext db) : ICalendarSource
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarItem>> GetItemsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var q = db.Reminders.AsNoTracking()
            .Where(r => r.HouseholdId == me.HouseholdId && r.RemindOn >= from && r.RemindOn <= to);

        q = me.IsManager
            ? q.Where(r => r.Visibility != Visibility.Private || r.OwnerId == me.Id || r.ForMemberId == me.Id)
            : q.Where(r => r.OwnerId == me.Id || r.ForMemberId == me.Id || r.Visibility == Visibility.Household);

        var reminders = await q.ToListAsync(ct);
        return reminders
            .Select(r => new CalendarItem("reminders", r.Id, r.Title, r.RemindOn, "reminder", r.RemindAt?.ToString("HH:mm"), r.IsDone))
            .ToList();
    }
}

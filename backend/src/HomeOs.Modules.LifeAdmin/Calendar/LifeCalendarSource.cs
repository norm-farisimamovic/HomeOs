using HomeOs.Modules.LifeAdmin.Persistence;
using HomeOs.Platform.Calendar;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.LifeAdmin.Calendar;

/// <summary>Contributes life-admin expiry/renewal dates to the shared calendar.</summary>
public sealed class LifeCalendarSource(ICurrentMember me, LifeAdminDbContext db) : ICalendarSource
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarItem>> GetItemsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var q = db.Records.AsNoTracking()
            .Where(r => r.HouseholdId == me.HouseholdId && r.ExpiresOn != null && r.ExpiresOn >= from && r.ExpiresOn <= to);

        q = me.IsManager
            ? q.Where(r => r.Visibility != Visibility.Private || r.OwnerId == me.Id)
            : q.Where(r => r.OwnerId == me.Id || r.Visibility == Visibility.Household);

        var records = await q.ToListAsync(ct);
        return records
            .Select(r => new CalendarItem("life", r.Id, r.Title, r.ExpiresOn!.Value, "renewal", null, false))
            .ToList();
    }
}

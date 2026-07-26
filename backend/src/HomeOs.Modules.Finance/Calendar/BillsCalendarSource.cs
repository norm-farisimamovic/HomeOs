using HomeOs.Modules.Finance.Persistence;
using HomeOs.Platform.Calendar;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Finance.Calendar;

/// <summary>Contributes bills' next-due dates to the shared calendar (respecting the member's visibility).</summary>
public sealed class BillsCalendarSource(ICurrentMember me, FinanceDbContext db) : ICalendarSource
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarItem>> GetItemsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var q = db.Bills.AsNoTracking()
            .Where(b => b.HouseholdId == me.HouseholdId && b.NextDue >= from && b.NextDue <= to);

        q = me.IsManager
            ? q.Where(b => b.Visibility != Visibility.Private || b.OwnerId == me.Id)
            : q.Where(b => b.OwnerId == me.Id || b.Visibility == Visibility.Household);

        var bills = await q.ToListAsync(ct);
        return bills
            .Select(b => new CalendarItem("finance", b.Id, b.Name, b.NextDue, "bill", null, false))
            .ToList();
    }
}

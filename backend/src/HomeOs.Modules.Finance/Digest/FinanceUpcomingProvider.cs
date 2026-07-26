using HomeOs.Modules.Finance.Persistence;
using HomeOs.Platform.Digest;
using HomeOs.Platform.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Finance.Digest;

/// <summary>Contributes a member's upcoming bills to their digest.</summary>
public sealed class FinanceUpcomingProvider(FinanceDbContext db) : IUpcomingProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<UpcomingItem>> GetUpcomingAsync(
        Guid householdId, Guid memberId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var bills = await db.Bills.AsNoTracking()
            .Where(b => b.HouseholdId == householdId && b.NextDue >= from && b.NextDue <= to
                && (b.OwnerId == memberId || b.WhoPaysId == memberId || b.Visibility == Visibility.Household))
            .ToListAsync(ct);

        return bills.Select(b => new UpcomingItem("finance", b.Name, b.NextDue, "bill")).ToList();
    }
}

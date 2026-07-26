using HomeOs.Modules.Finance.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using HomeOs.Platform.Search;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Finance.Search;

/// <summary>Makes bills findable in global search (name match, respecting visibility).</summary>
public sealed class FinanceSearchProvider(ICurrentMember me, FinanceDbContext db) : ISearchProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = query.ToLower();
        var bills = await db.Bills.AsNoTracking()
            .Where(b => b.HouseholdId == me.HouseholdId && b.Name.ToLower().Contains(q)
                        && (b.OwnerId == me.Id || b.Visibility == Visibility.Household
                            || (me.IsManager && b.Visibility != Visibility.Private)))
            .OrderBy(b => b.NextDue).Take(5).ToListAsync(cancellationToken);
        return bills.Select(b => new SearchHit("finance", b.Id, b.Name, b.Category, "/finance")).ToList();
    }
}

using HomeOs.Modules.LifeAdmin.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using HomeOs.Platform.Search;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.LifeAdmin.Search;

/// <summary>Makes life-admin records findable in global search (title/provider match, respecting visibility).</summary>
public sealed class LifeSearchProvider(ICurrentMember me, LifeAdminDbContext db) : ISearchProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = query.ToLower();
        var items = await db.Records.AsNoTracking()
            .Where(r => r.HouseholdId == me.HouseholdId && (r.Title.ToLower().Contains(q) || (r.Provider != null && r.Provider.ToLower().Contains(q)))
                        && (r.OwnerId == me.Id || r.Visibility == Visibility.Household
                            || (me.IsManager && r.Visibility != Visibility.Private)))
            .OrderBy(r => r.ExpiresOn).Take(5).ToListAsync(cancellationToken);
        return items.Select(r => new SearchHit("life", r.Id, r.Title, r.Provider, "/life")).ToList();
    }
}

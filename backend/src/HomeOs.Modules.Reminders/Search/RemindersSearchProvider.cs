using HomeOs.Modules.Reminders.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using HomeOs.Platform.Search;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Reminders.Search;

/// <summary>Makes reminders findable in global search (title match, respecting visibility).</summary>
public sealed class RemindersSearchProvider(ICurrentMember me, RemindersDbContext db) : ISearchProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = query.ToLower();
        var items = await db.Reminders.AsNoTracking()
            .Where(r => r.HouseholdId == me.HouseholdId && r.Title.ToLower().Contains(q)
                        && (r.OwnerId == me.Id || r.ForMemberId == me.Id || r.Visibility == Visibility.Household
                            || (me.IsManager && r.Visibility != Visibility.Private)))
            .OrderBy(r => r.RemindOn).Take(5).ToListAsync(cancellationToken);
        return items.Select(r => new SearchHit("reminders", r.Id, r.Title, r.RemindOn.ToString("yyyy-MM-dd"), "/reminders")).ToList();
    }
}

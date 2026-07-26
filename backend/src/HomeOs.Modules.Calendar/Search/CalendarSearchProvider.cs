using HomeOs.Modules.Calendar.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using HomeOs.Platform.Search;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Calendar.Search;

/// <summary>Makes calendar events findable in global search (title match, respecting visibility).</summary>
public sealed class CalendarSearchProvider(ICurrentMember me, CalendarDbContext db) : ISearchProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = query.ToLower();
        var events = await db.Events.AsNoTracking()
            .Where(e => e.HouseholdId == me.HouseholdId && e.Title.ToLower().Contains(q)
                        && (e.OwnerId == me.Id || e.Visibility == Visibility.Household
                            || (me.IsManager && e.Visibility != Visibility.Private)))
            .OrderBy(e => e.StartsOn).Take(5).ToListAsync(cancellationToken);
        return events.Select(e => new SearchHit("calendar", e.Id, e.Title, e.StartsOn.ToString("yyyy-MM-dd"), "/calendar")).ToList();
    }
}

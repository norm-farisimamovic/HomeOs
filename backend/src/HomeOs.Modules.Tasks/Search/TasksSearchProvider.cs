using HomeOs.Modules.Tasks.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using HomeOs.Platform.Search;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Tasks.Search;

/// <summary>Makes tasks findable in global search (title match, respecting visibility).</summary>
public sealed class TasksSearchProvider(ICurrentMember me, TasksDbContext db) : ISearchProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = query.ToLower();
        var items = await db.Tasks.AsNoTracking()
            .Where(t => t.HouseholdId == me.HouseholdId && t.Title.ToLower().Contains(q)
                        && (t.OwnerId == me.Id || t.AssigneeId == me.Id || t.Visibility == Visibility.Household
                            || (me.IsManager && t.Visibility != Visibility.Private)))
            .OrderByDescending(t => t.CreatedAtUtc).Take(5).ToListAsync(cancellationToken);
        return items.Select(t => new SearchHit("tasks", t.Id, t.Title, null, "/tasks")).ToList();
    }
}

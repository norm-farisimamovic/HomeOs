using HomeOs.Modules.Notes.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using HomeOs.Platform.Search;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Notes.Search;

/// <summary>Makes notes findable in global search (title/content match, respecting visibility).</summary>
public sealed class NotesSearchProvider(ICurrentMember me, NotesDbContext db) : ISearchProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = query.ToLower();
        var items = await db.Notes.AsNoTracking()
            .Where(n => n.HouseholdId == me.HouseholdId && (n.Title.ToLower().Contains(q) || n.Content.ToLower().Contains(q))
                        && (n.OwnerId == me.Id || n.Visibility == Visibility.Household
                            || (me.IsManager && n.Visibility != Visibility.Private)))
            .OrderByDescending(n => n.UpdatedAtUtc).Take(5).ToListAsync(cancellationToken);
        return items.Select(n => new SearchHit("notes", n.Id, n.Title, null, "/notes")).ToList();
    }
}

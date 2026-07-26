using HomeOs.Modules.Shopping.Persistence;
using HomeOs.Platform.Members;
using HomeOs.Platform.Search;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Shopping.Search;

/// <summary>Contributes shopping lists (and matching items) to global search.</summary>
public sealed class ShoppingSearchProvider(ICurrentMember me, ShoppingDbContext db) : ISearchProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken ct = default)
    {
        var lists = await db.Lists.AsNoTracking().Include(l => l.Items)
            .Where(l => l.HouseholdId == me.HouseholdId
                && (EF.Functions.Like(l.Name, $"%{query}%") || l.Items.Any(i => EF.Functions.Like(i.Text, $"%{query}%"))))
            .Take(5).ToListAsync(ct);

        return lists.Select(l => new SearchHit("shopping", l.Id, l.Name,
            l.Items.FirstOrDefault(i => i.Text.Contains(query, StringComparison.OrdinalIgnoreCase))?.Text, "/shopping")).ToList();
    }
}

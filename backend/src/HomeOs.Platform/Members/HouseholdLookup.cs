using HomeOs.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Members;

/// <summary>Kernel service to resolve a household id (used by seeders/tools). Apps don't touch the households table.</summary>
public interface IHouseholdLookup
{
    /// <summary>Finds a household id by exact name, or null.</summary>
    Task<Guid?> FindHouseholdIdByNameAsync(string name, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class HouseholdLookup(PlatformDbContext db) : IHouseholdLookup
{
    /// <inheritdoc />
    public async Task<Guid?> FindHouseholdIdByNameAsync(string name, CancellationToken ct = default) =>
        await db.Households.AsNoTracking()
            .Where(h => h.Name == name)
            .Select(h => (Guid?)h.Id)
            .FirstOrDefaultAsync(ct);
}

using HomeOs.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Links;

/// <summary>Kernel capability: link app objects to each other and read those links back.</summary>
public interface IEntityLinks
{
    /// <summary>Links from an object, newest first.</summary>
    Task<IReadOnlyList<EntityLink>> ForAsync(Guid householdId, string fromType, Guid fromId, CancellationToken ct = default);

    /// <summary>Creates a link (idempotent per from→to pair). Returns the existing or new link.</summary>
    Task<EntityLink> LinkAsync(Guid householdId, string fromType, Guid fromId,
        string toType, Guid toId, string toTitle, string toLink, CancellationToken ct = default);

    /// <summary>Removes a link owned by the household.</summary>
    Task UnlinkAsync(Guid householdId, Guid linkId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class EntityLinks(PlatformDbContext db) : IEntityLinks
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<EntityLink>> ForAsync(Guid householdId, string fromType, Guid fromId, CancellationToken ct = default) =>
        await db.EntityLinks.AsNoTracking()
            .Where(l => l.HouseholdId == householdId && l.FromType == fromType && l.FromId == fromId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<EntityLink> LinkAsync(Guid householdId, string fromType, Guid fromId,
        string toType, Guid toId, string toTitle, string toLink, CancellationToken ct = default)
    {
        var existing = await db.EntityLinks.FirstOrDefaultAsync(
            l => l.HouseholdId == householdId && l.FromType == fromType && l.FromId == fromId
                 && l.ToType == toType && l.ToId == toId, ct);
        if (existing is not null) return existing;

        var link = EntityLink.Create(householdId, fromType, fromId, toType, toId, toTitle, toLink);
        db.EntityLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return link;
    }

    /// <inheritdoc />
    public async Task UnlinkAsync(Guid householdId, Guid linkId, CancellationToken ct = default)
    {
        var link = await db.EntityLinks.FirstOrDefaultAsync(l => l.Id == linkId && l.HouseholdId == householdId, ct);
        if (link is null) return;
        db.EntityLinks.Remove(link);
        await db.SaveChangesAsync(ct);
    }
}

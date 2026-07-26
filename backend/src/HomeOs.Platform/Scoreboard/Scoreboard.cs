using HomeOs.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Scoreboard;

/// <summary>A member's standing on the household scoreboard.</summary>
public sealed record ScoreRow(Guid MemberId, int Points, int Count);

/// <summary>
/// Kernel gamification capability — award/revoke points for household activity (chores done, etc.) and read
/// the leaderboard. Apps depend only on this contract; a task app awards on completion without the kernel
/// knowing what a "task" is. Idempotent per (source key, source id).
/// </summary>
public interface IScoreboard
{
    /// <summary>Awards points once for a source (no-op if that source already scored).</summary>
    Task AwardAsync(Guid householdId, Guid memberId, string sourceKey, Guid sourceId, int points, CancellationToken ct = default);

    /// <summary>Removes the points previously awarded for a source (e.g. a task was un-completed).</summary>
    Task RevokeAsync(Guid householdId, string sourceKey, Guid sourceId, CancellationToken ct = default);

    /// <summary>The household leaderboard — points and count per member, highest first.</summary>
    Task<IReadOnlyList<ScoreRow>> GetAsync(Guid householdId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class Scoreboard(PlatformDbContext db) : IScoreboard
{
    /// <inheritdoc />
    public async Task AwardAsync(Guid householdId, Guid memberId, string sourceKey, Guid sourceId, int points, CancellationToken ct = default)
    {
        var exists = await db.PointsEntries.AnyAsync(p => p.HouseholdId == householdId && p.SourceKey == sourceKey && p.SourceId == sourceId, ct);
        if (exists) return;
        db.PointsEntries.Add(new PointsEntry { HouseholdId = householdId, MemberId = memberId, SourceKey = sourceKey, SourceId = sourceId, Points = points });
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RevokeAsync(Guid householdId, string sourceKey, Guid sourceId, CancellationToken ct = default)
    {
        var entry = await db.PointsEntries.FirstOrDefaultAsync(p => p.HouseholdId == householdId && p.SourceKey == sourceKey && p.SourceId == sourceId, ct);
        if (entry is null) return;
        db.PointsEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScoreRow>> GetAsync(Guid householdId, CancellationToken ct = default)
    {
        // Group to an anonymous type (EF translates this), then map to the record client-side.
        var rows = await db.PointsEntries.AsNoTracking()
            .Where(p => p.HouseholdId == householdId)
            .GroupBy(p => p.MemberId)
            .Select(g => new { MemberId = g.Key, Points = g.Sum(x => x.Points), Count = g.Count() })
            .OrderByDescending(r => r.Points)
            .ToListAsync(ct);
        return rows.Select(r => new ScoreRow(r.MemberId, r.Points, r.Count)).ToList();
    }
}

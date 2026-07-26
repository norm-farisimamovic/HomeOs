using HomeOs.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Members;

/// <summary>
/// A household member, as seen by other apps (kernel capability — apps never read the user table).
/// <c>PreferredCulture</c> (e.g. <c>bs</c>/<c>en</c>) lets apps localize emails to that member.
/// </summary>
public sealed record MemberSummary(Guid Id, string DisplayName, string Email, string PreferredCulture);

/// <summary>Kernel service so any app can list / name household members without importing identity.</summary>
public interface IMemberDirectory
{
    /// <summary>All members of a household.</summary>
    Task<IReadOnlyList<MemberSummary>> GetHouseholdMembersAsync(Guid householdId, CancellationToken ct = default);

    /// <summary>Member-id → display-name map for a household (for enriching DTOs).</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(Guid householdId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class MemberDirectory(PlatformDbContext db) : IMemberDirectory
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<MemberSummary>> GetHouseholdMembersAsync(Guid householdId, CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => u.HouseholdId == householdId)
            .OrderBy(u => u.DisplayName)
            .Select(u => new MemberSummary(u.Id, u.DisplayName, u.Email ?? string.Empty, u.PreferredCulture))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(Guid householdId, CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => u.HouseholdId == householdId)
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
}

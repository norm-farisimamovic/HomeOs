using System.Text.Json;
using HomeOs.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Apps;

/// <summary>An app's effective state for a household: its manifest plus enablement and granted capabilities.</summary>
public sealed record AppState(AppManifest Manifest, bool Enabled, IReadOnlyList<string> GrantedCapabilities);

/// <summary>
/// The household's control panel for apps: what's enabled and what each app is allowed to do. Enforcement
/// (middleware, search/calendar aggregation) asks this; the Apps page reads and changes it. Defaults keep a
/// new app working immediately (enabled, all capabilities granted) until the household narrows it.
/// </summary>
public interface IAppAccess
{
    /// <summary>Whether the app is enabled for the household (core apps are always enabled).</summary>
    Task<bool> IsEnabledAsync(Guid householdId, string appId, CancellationToken ct = default);

    /// <summary>Whether the household has granted the app a capability (core apps always have all).</summary>
    Task<bool> HasCapabilityAsync(Guid householdId, string appId, string capability, CancellationToken ct = default);

    /// <summary>The set of app ids currently enabled for the household (for filtering shared surfaces).</summary>
    Task<IReadOnlySet<string>> EnabledAppIdsAsync(Guid householdId, CancellationToken ct = default);

    /// <summary>Every app's effective state for the household (for the Apps page).</summary>
    Task<IReadOnlyList<AppState>> ListAsync(Guid householdId, CancellationToken ct = default);

    /// <summary>Enables or disables a non-core app.</summary>
    Task SetEnabledAsync(Guid householdId, string appId, bool enabled, CancellationToken ct = default);

    /// <summary>Replaces the capabilities granted to a non-core app (intersected with what it declares).</summary>
    Task SetCapabilitiesAsync(Guid householdId, string appId, IReadOnlyList<string> capabilities, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AppAccess(PlatformDbContext db, IAppRegistry registry) : IAppAccess
{
    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(Guid householdId, string appId, CancellationToken ct = default)
    {
        var manifest = registry.ById(appId);
        if (manifest is null) return false;
        if (manifest.IsCore) return true;
        var row = await Row(householdId, appId, ct);
        return row?.Enabled ?? true; // default: installed
    }

    /// <inheritdoc />
    public async Task<bool> HasCapabilityAsync(Guid householdId, string appId, string capability, CancellationToken ct = default)
    {
        var manifest = registry.ById(appId);
        if (manifest is null) return false;
        if (manifest.IsCore) return true;
        var row = await Row(householdId, appId, ct);
        // No row → default grant of everything the manifest declares.
        var granted = row is null ? manifest.Capabilities : Deserialize(row.GrantedCapabilities);
        return granted.Contains(capability);
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> EnabledAppIdsAsync(Guid householdId, CancellationToken ct = default)
    {
        var rows = await db.HouseholdApps.AsNoTracking()
            .Where(a => a.HouseholdId == householdId).ToListAsync(ct);
        var byId = rows.ToDictionary(r => r.AppId);
        var enabled = registry.All
            .Where(m => m.IsCore || (byId.TryGetValue(m.Id, out var r) ? r.Enabled : true))
            .Select(m => m.Id);
        return enabled.ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppState>> ListAsync(Guid householdId, CancellationToken ct = default)
    {
        var rows = await db.HouseholdApps.AsNoTracking()
            .Where(a => a.HouseholdId == householdId).ToListAsync(ct);
        var byId = rows.ToDictionary(r => r.AppId);

        return registry.All.Select(m =>
        {
            if (m.IsCore) return new AppState(m, true, m.Capabilities);
            if (!byId.TryGetValue(m.Id, out var row)) return new AppState(m, true, m.Capabilities);
            // Keep only capabilities the manifest still declares (drops stale grants).
            var granted = Deserialize(row.GrantedCapabilities).Where(m.Capabilities.Contains).ToList();
            return new AppState(m, row.Enabled, granted);
        }).ToList();
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(Guid householdId, string appId, bool enabled, CancellationToken ct = default)
    {
        var manifest = registry.ById(appId)
            ?? throw new InvalidOperationException($"Unknown app '{appId}'.");
        if (manifest.IsCore) throw new InvalidOperationException("Core apps can't be disabled.");

        var row = await db.HouseholdApps.FirstOrDefaultAsync(a => a.HouseholdId == householdId && a.AppId == appId, ct);
        if (row is null)
        {
            row = HouseholdApp.Create(householdId, appId, enabled, Serialize(manifest.Capabilities));
            db.HouseholdApps.Add(row);
        }
        else
        {
            row.SetEnabled(enabled);
        }
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task SetCapabilitiesAsync(Guid householdId, string appId, IReadOnlyList<string> capabilities, CancellationToken ct = default)
    {
        var manifest = registry.ById(appId)
            ?? throw new InvalidOperationException($"Unknown app '{appId}'.");
        if (manifest.IsCore) throw new InvalidOperationException("Core apps have fixed capabilities.");

        // Only ever store capabilities the app actually declares — extensibility can't widen access.
        var clean = capabilities.Where(manifest.Capabilities.Contains).Distinct().ToList();

        var row = await db.HouseholdApps.FirstOrDefaultAsync(a => a.HouseholdId == householdId && a.AppId == appId, ct);
        if (row is null)
        {
            row = HouseholdApp.Create(householdId, appId, true, Serialize(clean));
            db.HouseholdApps.Add(row);
        }
        else
        {
            row.SetCapabilities(Serialize(clean));
        }
        await db.SaveChangesAsync(ct);
    }

    private Task<HouseholdApp?> Row(Guid householdId, string appId, CancellationToken ct) =>
        db.HouseholdApps.AsNoTracking().FirstOrDefaultAsync(a => a.HouseholdId == householdId && a.AppId == appId, ct);

    private static IReadOnlyList<string> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string Serialize(IEnumerable<string> caps) => JsonSerializer.Serialize(caps);
}

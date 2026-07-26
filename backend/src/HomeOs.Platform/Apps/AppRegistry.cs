using Microsoft.AspNetCore.Http;

namespace HomeOs.Platform.Apps;

/// <summary>The catalogue of every installed app — core platform surfaces plus module-provided apps.</summary>
public interface IAppRegistry
{
    /// <summary>Every known manifest (core first, then apps), ordered stably.</summary>
    IReadOnlyList<AppManifest> All { get; }

    /// <summary>The manifest owning an API path (longest <c>ApiPrefix</c> match), or <c>null</c>.</summary>
    AppManifest? ForApiPath(PathString path);

    /// <summary>The manifest with the given id, or <c>null</c>.</summary>
    AppManifest? ById(string id);
}

/// <summary>
/// Aggregates the core platform surfaces (defined here — always present, never disabled) with every module's
/// <see cref="IAppModule"/>. Add an app module and it shows up on nav, the Apps page, and in enforcement with
/// no change here.
/// </summary>
public sealed class AppRegistry : IAppRegistry
{
    // Core surfaces the platform always provides. They can't be disabled, so they carry no capabilities.
    private static readonly AppManifest[] Core =
    [
        new("dashboard", "nav.today", "apps.desc.dashboard", "Home", "var(--brand)", "/", null, [], IsCore: true),
        new("household", "nav.household", "apps.desc.household", "Users", "var(--text-3)", "/household", null, [], IsCore: true),
        new("notifications", "nav.notifications", "apps.desc.notifications", "Mail", "var(--text-3)", "/notifications", null, [], IsCore: true),
        new("audit", "nav.audit", "apps.desc.audit", "ScrollText", "var(--text-3)", "/audit", null, [], IsCore: true),
        new("apps", "nav.apps", "apps.desc.apps", "Blocks", "var(--text-3)", "/apps", null, [], IsCore: true),
        new("settings", "nav.settings", "apps.desc.settings", "Settings", "var(--text-3)", "/settings", null, [], IsCore: true),
    ];

    private readonly IReadOnlyList<AppManifest> _all;

    /// <summary>Builds the registry from the core surfaces and the registered app modules.</summary>
    public AppRegistry(IEnumerable<IAppModule> modules)
    {
        var apps = modules.Select(m => m.Manifest).OrderBy(m => m.Id, StringComparer.Ordinal);
        _all = [.. Core, .. apps];
    }

    /// <inheritdoc />
    public IReadOnlyList<AppManifest> All => _all;

    /// <inheritdoc />
    public AppManifest? ForApiPath(PathString path) => _all
        .Where(m => m.ApiPrefix is not null &&
                    path.StartsWithSegments(m.ApiPrefix, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(m => m.ApiPrefix!.Length)
        .FirstOrDefault();

    /// <inheritdoc />
    public AppManifest? ById(string id) => _all.FirstOrDefault(m => m.Id == id);
}

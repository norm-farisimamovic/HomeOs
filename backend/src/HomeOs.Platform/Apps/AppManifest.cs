namespace HomeOs.Platform.Apps;

/// <summary>
/// An app's self-description — the same shape for a built-in app and a brand-new one. Modules provide it via
/// <see cref="IAppModule"/>; the platform uses it to drive navigation, the Apps page, and access enforcement,
/// so an app appears everywhere the built-ins do the moment it registers, with no special-casing.
/// </summary>
/// <param name="Id">Stable slug, matches the search/calendar source key (e.g. <c>tasks</c>, <c>finance</c>).</param>
/// <param name="NameKey">i18n key for the display name (e.g. <c>nav.tasks</c>).</param>
/// <param name="DescriptionKey">i18n key for the one-line description shown on the Apps page.</param>
/// <param name="Icon">lucide-react icon name the frontend renders (e.g. <c>CheckSquare</c>).</param>
/// <param name="Hue">CSS colour variable for the app's accent (e.g. <c>var(--m-tasks)</c>).</param>
/// <param name="Route">Frontend route (e.g. <c>/tasks</c>).</param>
/// <param name="ApiPrefix">API path the app owns (e.g. <c>/api/tasks</c>); <c>null</c> for frontend-only apps (Kanban).</param>
/// <param name="Capabilities">Abilities the household grants this app (e.g. <c>read:tasks</c>, <c>write:tasks</c>).</param>
/// <param name="IsCore">Core platform surfaces (Dashboard, Household…) that can't be disabled or uninstalled.</param>
public sealed record AppManifest(
    string Id,
    string NameKey,
    string DescriptionKey,
    string Icon,
    string Hue,
    string Route,
    string? ApiPrefix,
    IReadOnlyList<string> Capabilities,
    bool IsCore = false)
{
    /// <summary>The capability required to read this app's data (used by access enforcement).</summary>
    public string ReadCapability => $"read:{Id}";

    /// <summary>The capability required to change this app's data (used by access enforcement).</summary>
    public string WriteCapability => $"write:{Id}";
}

/// <summary>
/// Implemented by every app module so it announces itself to the platform. Register with
/// <c>services.AddSingleton&lt;IAppModule, TasksAppModule&gt;()</c> — being extendable is part of an app's job,
/// not an afterthought.
/// </summary>
public interface IAppModule
{
    /// <summary>This app's manifest.</summary>
    AppManifest Manifest { get; }
}

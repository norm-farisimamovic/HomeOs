namespace HomeOs.Platform.Search;

/// <summary>One search result contributed by an app. The palette groups by <see cref="Source"/>.</summary>
/// <param name="Source">Owning app key (<c>tasks</c>, <c>notes</c>…) — the UI colours/labels by this.</param>
/// <param name="Id">The object's id.</param>
/// <param name="Title">Primary label.</param>
/// <param name="Subtitle">Optional secondary line (date, category…).</param>
/// <param name="Link">In-app route to open it.</param>
public sealed record SearchHit(string Source, Guid Id, string Title, string? Subtitle, string Link);

/// <summary>
/// Implemented by any app that has searchable content. Registered scoped; resolves the current member
/// itself and returns only what they may see. Global search injects every provider and merges — so a
/// new app appears in search the moment it registers one, with no changes to the search surface.
/// </summary>
public interface ISearchProvider
{
    /// <summary>Up to a handful of matches for <paramref name="query"/> visible to the current member.</summary>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

using HomeOs.Platform.Apps;
using HomeOs.Platform.Members;
using HomeOs.Platform.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HomeOs.Platform.Features.Search;

/// <summary>Global search — merges every registered <see cref="ISearchProvider"/> for the current member.</summary>
public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/search", async (string? q, IEnumerable<ISearchProvider> providers,
            ICurrentMember me, IAppAccess access, CancellationToken ct) =>
            {
                var query = (q ?? string.Empty).Trim();
                if (query.Length < 2) return Results.Ok(Array.Empty<SearchHit>());

                var hits = new List<SearchHit>();
                foreach (var provider in providers)
                    hits.AddRange(await provider.SearchAsync(query, ct));

                // Hide results from apps the household has disabled (a hit's Source is the app id).
                var enabled = await access.EnabledAppIdsAsync(me.HouseholdId, ct);
                return Results.Ok(hits.Where(h => enabled.Contains(h.Source)).Take(30).ToList());
            })
            .RequireAuthorization()
            .WithTags("Search")
            .WithName("Search");

        return app;
    }
}

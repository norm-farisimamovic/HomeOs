using HomeOs.Platform.Members;
using HomeOs.Platform.Scoreboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HomeOs.Platform.Features.Scoreboard;

/// <summary>Household scoreboard — points earned by completing chores and other activity.</summary>
public static class ScoreboardEndpoints
{
    public static IEndpointRouteBuilder MapScoreboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/scoreboard", async (ICurrentMember me, IScoreboard scoreboard, IMemberDirectory dir, CancellationToken ct) =>
        {
            var rows = await scoreboard.GetAsync(me.HouseholdId, ct);
            var names = await dir.GetNamesAsync(me.HouseholdId, ct);
            var result = rows.Select(r => new ScoreEntryDto(r.MemberId, names.GetValueOrDefault(r.MemberId, ""), r.Points, r.Count)).ToList();
            return Results.Ok(result);
        }).RequireAuthorization().WithTags("Scoreboard").WithName("GetScoreboard");

        return app;
    }
}

/// <summary>One member's scoreboard standing for the client.</summary>
public sealed record ScoreEntryDto(Guid MemberId, string MemberName, int Points, int Count);

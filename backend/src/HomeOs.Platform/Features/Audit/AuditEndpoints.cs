using System.Security.Claims;
using HomeOs.Platform.Access;
using HomeOs.Platform.Members;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Features.Audit;

/// <summary>The household audit log — visible to Owner/Admin only.</summary>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit", async (ClaimsPrincipal user, ICurrentMember me, PlatformDbContext db, IMemberDirectory dir, CancellationToken ct) =>
            {
                if (!(user.IsInRole(HouseholdRoles.Owner) || user.IsInRole(HouseholdRoles.Admin)))
                    return Results.Forbid();

                var entries = await db.AuditEntries.AsNoTracking()
                    .Where(a => a.HouseholdId == me.HouseholdId)
                    .OrderByDescending(a => a.CreatedAtUtc)
                    .Take(100).ToListAsync(ct);
                var names = await dir.GetNamesAsync(me.HouseholdId, ct);

                var dtos = entries.Select(a => new AuditEntryDto(
                    a.Id, a.Action, a.Detail,
                    a.ActorMemberId is { } id ? names.GetValueOrDefault(id) : null,
                    a.CreatedAtUtc.ToString("o"))).ToList();
                return Results.Ok(dtos);
            })
            .RequireAuthorization().WithTags("Audit").WithName("AuditLog");

        return app;
    }
}

/// <summary>An audit entry for the client.</summary>
public sealed record AuditEntryDto(Guid Id, string Action, string Detail, string? ActorName, string CreatedAt);

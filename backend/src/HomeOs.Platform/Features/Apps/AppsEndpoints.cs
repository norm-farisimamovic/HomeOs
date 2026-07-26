using HomeOs.Platform.Access;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Audit;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace HomeOs.Platform.Features.Apps;

/// <summary>The household's app catalogue + control panel. Everyone can read it; only Owner/Admin can change it.</summary>
public static class AppsEndpoints
{
    public static IEndpointRouteBuilder MapAppsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/apps").RequireAuthorization().WithTags("Apps");

        // List every app with its effective state for this household.
        group.MapGet("/", async (ICurrentMember me, IAppAccess access, CancellationToken ct) =>
        {
            var states = await access.ListAsync(me.HouseholdId, ct);
            return Results.Ok(states.Select(ToDto).ToList());
        }).WithName("ListApps");

        // Enable / disable a non-core app (install / uninstall for the household).
        group.MapPut("/{id}/enabled", async (
            string id, SetEnabledRequest req, ClaimsPrincipal user,
            ICurrentMember me, IAppAccess access, IAppRegistry registry, IAuditLog audit, IAppText text, CancellationToken ct) =>
        {
            if (!IsManager(user)) return Results.Forbid();
            var manifest = registry.ById(id);
            if (manifest is null) return Results.NotFound();
            if (manifest.IsCore) return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: text["error.app.core"]);

            await access.SetEnabledAsync(me.HouseholdId, id, req.Enabled, ct);
            await audit.RecordAsync(req.Enabled ? "app.enabled" : "app.disabled", id, ct);
            return Results.NoContent();
        }).WithName("SetAppEnabled");

        // Replace the capabilities granted to a non-core app.
        group.MapPut("/{id}/capabilities", async (
            string id, SetCapabilitiesRequest req, ClaimsPrincipal user,
            ICurrentMember me, IAppAccess access, IAppRegistry registry, IAuditLog audit, IAppText text, CancellationToken ct) =>
        {
            if (!IsManager(user)) return Results.Forbid();
            var manifest = registry.ById(id);
            if (manifest is null) return Results.NotFound();
            if (manifest.IsCore) return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: text["error.app.core"]);

            await access.SetCapabilitiesAsync(me.HouseholdId, id, req.Capabilities ?? [], ct);
            await audit.RecordAsync("app.capabilitiesChanged", id, ct);
            return Results.NoContent();
        }).WithName("SetAppCapabilities");

        return app;
    }

    private static bool IsManager(ClaimsPrincipal user) =>
        user.IsInRole(HouseholdRoles.Owner) || user.IsInRole(HouseholdRoles.Admin);

    private static AppDto ToDto(AppState s) => new(
        s.Manifest.Id, s.Manifest.NameKey, s.Manifest.DescriptionKey, s.Manifest.Icon, s.Manifest.Hue,
        s.Manifest.Route, s.Manifest.IsCore, s.Enabled, s.Manifest.Capabilities, s.GrantedCapabilities);
}

/// <summary>An app and its state for the current household.</summary>
public sealed record AppDto(
    string Id, string NameKey, string DescriptionKey, string Icon, string Hue, string Route,
    bool IsCore, bool Enabled, IReadOnlyList<string> Capabilities, IReadOnlyList<string> GrantedCapabilities);

/// <summary>Enable/disable payload.</summary>
public sealed record SetEnabledRequest(bool Enabled);

/// <summary>Capability-grant payload.</summary>
public sealed record SetCapabilitiesRequest(IReadOnlyList<string>? Capabilities);

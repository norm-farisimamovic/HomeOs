using HomeOs.Modules.Automations.Domain;
using HomeOs.Modules.Automations.Persistence;
using HomeOs.Platform.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Automations.Features;

/// <summary>Automations CRUD — the household's "when this, then that" rules.</summary>
public static class AutomationsEndpoints
{
    /// <summary>Triggers the UI offers (must match the <c>AppActivity.Kind</c> values apps publish).</summary>
    private static readonly string[] Triggers = ["task.completed", "bill.added", "event.scheduled", "chat.message"];
    private static readonly string[] Actions = ["notify"];

    public static IEndpointRouteBuilder MapAutomationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/automations").RequireAuthorization().WithTags("Automations");

        group.MapGet("/", ListAsync).WithName("ListAutomations");
        group.MapPost("/", CreateAsync).WithName("CreateAutomation");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateAutomation");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteAutomation");

        return app;
    }

    private static async Task<IResult> ListAsync(ICurrentMember me, AutomationsDbContext db, CancellationToken ct)
    {
        var rules = await db.Automations.AsNoTracking()
            .Where(a => a.HouseholdId == me.HouseholdId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AutomationDto(a.Id, a.Name, a.Trigger, a.Action, a.Message, a.Enabled, a.OwnerId, a.OwnerId == me.Id))
            .ToListAsync(ct);
        return Results.Ok(rules);
    }

    private static async Task<IResult> CreateAsync(SaveAutomationRequest req, ICurrentMember me, AutomationsDbContext db, CancellationToken ct)
    {
        if (Validate(req) is { } bad) return bad;
        var rule = Automation.Create(me.HouseholdId, me.Id, req.Name, req.Trigger, req.Action, req.Message);
        db.Automations.Add(rule);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/automations/{rule.Id}", ToDto(rule, me));
    }

    private static async Task<IResult> UpdateAsync(Guid id, SaveAutomationRequest req, ICurrentMember me, AutomationsDbContext db, CancellationToken ct)
    {
        if (Validate(req) is { } bad) return bad;
        var rule = await db.Automations.FirstOrDefaultAsync(a => a.Id == id && a.HouseholdId == me.HouseholdId, ct);
        if (rule is null) return Results.NotFound();
        if (rule.OwnerId != me.Id && !me.IsManager) return Results.Forbid();
        rule.Update(req.Name, req.Trigger, req.Action, req.Message, req.Enabled ?? rule.Enabled);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(rule, me));
    }

    private static async Task<IResult> DeleteAsync(Guid id, ICurrentMember me, AutomationsDbContext db, CancellationToken ct)
    {
        var rule = await db.Automations.FirstOrDefaultAsync(a => a.Id == id && a.HouseholdId == me.HouseholdId, ct);
        if (rule is null) return Results.NotFound();
        if (rule.OwnerId != me.Id && !me.IsManager) return Results.Forbid();
        db.Automations.Remove(rule);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static IResult? Validate(SaveAutomationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || !Triggers.Contains(req.Trigger) || !Actions.Contains(req.Action))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Invalid automation."] });
        return null;
    }

    private static AutomationDto ToDto(Automation a, ICurrentMember me) =>
        new(a.Id, a.Name, a.Trigger, a.Action, a.Message, a.Enabled, a.OwnerId, a.OwnerId == me.Id || me.IsManager);
}

/// <summary>An automation rule for the client.</summary>
public sealed record AutomationDto(Guid Id, string Name, string Trigger, string Action, string? Message, bool Enabled, Guid OwnerId, bool CanEdit);

/// <summary>Create/update payload.</summary>
public sealed record SaveAutomationRequest(string Name, string Trigger, string Action, string? Message, bool? Enabled);

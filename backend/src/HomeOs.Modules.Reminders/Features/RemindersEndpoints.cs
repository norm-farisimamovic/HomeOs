using System.Globalization;
using HomeOs.Modules.Reminders.Contracts;
using HomeOs.Modules.Reminders.Domain;
using HomeOs.Modules.Reminders.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Events;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Reminders.Features;

/// <summary>Reminders — dated nudges aimed at a member; household-scoped, member-visible, event-publishing.</summary>
public static class RemindersEndpoints
{
    public static IEndpointRouteBuilder MapRemindersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reminders").RequireAuthorization().WithTags("Reminders");

        group.MapGet("/", ListAsync).WithName("ListReminders");
        group.MapPost("/", CreateAsync).WithName("CreateReminder");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateReminder");
        group.MapPost("/{id:guid}/toggle", ToggleAsync).WithName("ToggleReminder");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteReminder");

        return app;
    }

    private static async Task<IResult> ListAsync(ICurrentMember me, RemindersDbContext db, IMemberDirectory dir, CancellationToken ct)
    {
        var today = Today();
        var reminders = await VisibleTo(db, me)
            .OrderBy(r => r.IsDone).ThenBy(r => r.RemindOn).ThenBy(r => r.RemindAt)
            .ToListAsync(ct);
        var names = await dir.GetNamesAsync(me.HouseholdId, ct);
        return Results.Ok(reminders.Select(r => ToDto(r, today, names, me)).ToList());
    }

    private static async Task<IResult> CreateAsync(
        SaveReminderRequest req, ICurrentMember me, RemindersDbContext db, IMemberDirectory dir, IEventBus bus, IAppText text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || ParseDate(req.RemindOn) is not { } remindOn)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.reminder.required"]] });

        var reminder = Reminder.Create(me.HouseholdId, me.Id, req.ForMemberId ?? me.Id, req.Title,
            remindOn, ParseTime(req.RemindAt), req.Notes, ParseVisibility(req.Visibility), ParseRecurrence(req.Recurrence));

        db.Reminders.Add(reminder);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new ReminderCreated(reminder.Id, reminder.HouseholdId, reminder.ForMemberId, reminder.Title, reminder.RemindOn), ct);

        var names = await dir.GetNamesAsync(me.HouseholdId, ct);
        return Results.Created($"/api/reminders/{reminder.Id}", ToDto(reminder, Today(), names, me));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, SaveReminderRequest req, ICurrentMember me, RemindersDbContext db, IMemberDirectory dir, IAppText text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || ParseDate(req.RemindOn) is not { } remindOn)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.reminder.required"]] });

        var reminder = await Editable(db, me, id, ct);
        if (reminder is null) return Results.NotFound();

        reminder.Update(req.ForMemberId ?? reminder.ForMemberId, req.Title, remindOn, ParseTime(req.RemindAt), req.Notes, ParseVisibility(req.Visibility), ParseRecurrence(req.Recurrence));
        await db.SaveChangesAsync(ct);
        var names = await dir.GetNamesAsync(me.HouseholdId, ct);
        return Results.Ok(ToDto(reminder, Today(), names, me));
    }

    private static async Task<IResult> ToggleAsync(Guid id, ICurrentMember me, RemindersDbContext db, IMemberDirectory dir, CancellationToken ct)
    {
        // The owner, the target member, or a manager may tick a reminder off/on.
        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == me.HouseholdId, ct);
        if (reminder is null || !CanToggle(me, reminder)) return Results.NotFound();

        if (reminder.IsDone) reminder.Reopen(); else reminder.Complete(Today());
        await db.SaveChangesAsync(ct);
        var names = await dir.GetNamesAsync(me.HouseholdId, ct);
        return Results.Ok(ToDto(reminder, Today(), names, me));
    }

    private static async Task<IResult> DeleteAsync(Guid id, ICurrentMember me, RemindersDbContext db, CancellationToken ct)
    {
        var reminder = await Editable(db, me, id, ct);
        if (reminder is null) return Results.NotFound();
        db.Reminders.Remove(reminder);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ---- helpers ----
    private static IQueryable<Reminder> VisibleTo(RemindersDbContext db, ICurrentMember me)
    {
        var q = db.Reminders.AsNoTracking().Where(r => r.HouseholdId == me.HouseholdId);
        return me.IsManager
            ? q.Where(r => r.Visibility != Visibility.Private || r.OwnerId == me.Id || r.ForMemberId == me.Id)
            : q.Where(r => r.OwnerId == me.Id || r.ForMemberId == me.Id || r.Visibility == Visibility.Household);
    }

    private static async Task<Reminder?> Editable(RemindersDbContext db, ICurrentMember me, Guid id, CancellationToken ct)
    {
        var r = await db.Reminders.FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == me.HouseholdId, ct);
        if (r is null) return null;
        if (me.IsManager && r.Visibility != Visibility.Private) return r;
        return r.OwnerId == me.Id ? r : null;
    }

    private static bool CanToggle(ICurrentMember me, Reminder r) =>
        r.OwnerId == me.Id || r.ForMemberId == me.Id || (me.IsManager && r.Visibility != Visibility.Private);

    private static ReminderDto ToDto(Reminder r, DateOnly today, IReadOnlyDictionary<Guid, string> names, ICurrentMember me) => new(
        r.Id, r.Title, r.RemindOn.ToString("yyyy-MM-dd"), r.RemindAt?.ToString("HH:mm"), r.Notes,
        r.ForMemberId, names.GetValueOrDefault(r.ForMemberId),
        r.Visibility.ToString(), r.Recurrence.ToString(), r.IsDone, !r.IsDone && r.RemindOn < today,
        r.OwnerId, (me.IsManager && r.Visibility != Visibility.Private) || r.OwnerId == me.Id);

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
    private static DateOnly? ParseDate(string? v) => DateOnly.TryParse(v, out var d) ? d : null;
    private static TimeOnly? ParseTime(string? v) => TimeOnly.TryParse(v, CultureInfo.InvariantCulture, out var t) ? t : null;
    private static Visibility ParseVisibility(string? v) => Enum.TryParse<Visibility>(v, true, out var x) ? x : Visibility.Private;
    private static Recurrence ParseRecurrence(string? v) => Enum.TryParse<Recurrence>(v, true, out var x) ? x : Recurrence.None;
}

using System.Globalization;
using HomeOs.Modules.Calendar.Contracts;
using HomeOs.Modules.Calendar.Domain;
using HomeOs.Modules.Calendar.Persistence;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Calendar;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Events;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Calendar.Features;

/// <summary>Calendar: own events (CRUD) plus a month feed that merges every app's dated items.</summary>
public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/calendar").RequireAuthorization().WithTags("Calendar");

        group.MapGet("/month", MonthAsync).WithName("CalendarMonth");
        group.MapGet("/events", ListEventsAsync).WithName("ListEvents");
        group.MapPost("/events", CreateEventAsync).WithName("CreateEvent");
        group.MapPut("/events/{id:guid}", UpdateEventAsync).WithName("UpdateEvent");
        group.MapDelete("/events/{id:guid}", DeleteEventAsync).WithName("DeleteEvent");

        return app;
    }

    // The "everything connects" endpoint: own events + every registered ICalendarSource (tasks, bills, …),
    // merged for the current member — with zero references to those other modules.
    private static async Task<IResult> MonthAsync(
        int year, int month, ICurrentMember me, CalendarDbContext db,
        IEnumerable<ICalendarSource> sources, IAppAccess access, CancellationToken ct)
    {
        if (month is < 1 or > 12) month = DateTime.UtcNow.Month;
        if (year is < 1 or > 9999) year = DateTime.UtcNow.Year;
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var events = (await VisibleQuery(db, me)
            .Where(e => e.StartsOn >= from && e.StartsOn <= to)
            .ToListAsync(ct))
            .Where(e => CanSee(e, me))
            .ToList();

        var items = events.Select(e => new CalendarFeedItem(
            "calendar", e.Id, e.Title, e.StartsOn.ToString("yyyy-MM-dd"), "event",
            e.StartTime?.ToString("HH:mm"), false)).ToList();

        // Fold in every other app's dated items (tasks due, bills due, …), skipping apps the household disabled.
        var enabled = await access.EnabledAppIdsAsync(me.HouseholdId, ct);
        foreach (var source in sources)
        {
            var contributed = await source.GetItemsAsync(from, to, ct);
            items.AddRange(contributed
                .Where(i => enabled.Contains(i.Source))
                .Select(i => new CalendarFeedItem(
                    i.Source, i.Id, i.Title, i.Date.ToString("yyyy-MM-dd"), i.Kind, i.Time, i.IsDone)));
        }

        items = items.OrderBy(i => i.Date).ThenBy(i => i.Time ?? "99:99").ToList();
        return Results.Ok(new MonthFeedDto(year, month, items));
    }

    private static async Task<IResult> ListEventsAsync(ICurrentMember me, CalendarDbContext db, IMemberDirectory dir, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var events = (await VisibleQuery(db, me)
            .Where(e => e.StartsOn >= today)
            .OrderBy(e => e.StartsOn).ThenBy(e => e.StartTime)
            .ToListAsync(ct))
            .Where(e => CanSee(e, me))
            .ToList();
        return Results.Ok(events.Select(e => ToDto(e, me)).ToList());
    }

    private static async Task<IResult> CreateEventAsync(
        SaveEventRequest req, ICurrentMember me, CalendarDbContext db, IEventBus bus, IShareNotifier share, IAppText text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || ParseDate(req.StartsOn) is not { } startsOn)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.calendar.required"]] });

        var ev = CalendarEvent.Create(me.HouseholdId, me.Id, req.Title, startsOn,
            ParseTime(req.StartTime), req.Location, req.Notes, ParseVisibility(req.Visibility), req.SharedWith);

        db.Events.Add(ev);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new EventScheduled(ev.Id, ev.HouseholdId, ev.Title, ev.StartsOn), ct);
        await bus.PublishAsync(new AppActivity(ev.HouseholdId, me.Id, "event.scheduled", ev.Title, "/calendar"), ct);
        await share.NotifySharedAsync(ev.HouseholdId, me.Id, ev.SharedWith, ev.Title, "/calendar", ct);
        return Results.Created($"/api/calendar/events/{ev.Id}", ToDto(ev, me));
    }

    private static async Task<IResult> UpdateEventAsync(
        Guid id, SaveEventRequest req, ICurrentMember me, CalendarDbContext db, IShareNotifier share, IAppText text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || ParseDate(req.StartsOn) is not { } startsOn)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.calendar.required"]] });

        var ev = await Editable(db, me, id, ct);
        if (ev is null) return Results.NotFound();

        var before = ev.SharedWith.ToHashSet();
        ev.Update(req.Title, startsOn, ParseTime(req.StartTime), req.Location, req.Notes, ParseVisibility(req.Visibility), req.SharedWith);
        await db.SaveChangesAsync(ct);
        // Only ping members who are newly shared with (not on every edit).
        await share.NotifySharedAsync(ev.HouseholdId, me.Id, ev.SharedWith.Where(x => !before.Contains(x)), ev.Title, "/calendar", ct);
        return Results.Ok(ToDto(ev, me));
    }

    private static async Task<IResult> DeleteEventAsync(Guid id, ICurrentMember me, CalendarDbContext db, CancellationToken ct)
    {
        var ev = await Editable(db, me, id, ct);
        if (ev is null) return Results.NotFound();
        db.Events.Remove(ev);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ---- helpers (same role-based visibility as the rest of the platform) ----
    // A translatable coarse pre-filter (Pomelo can't query the JSON SharedWith collection). We fetch this
    // superset, then refine with CanSee() in memory. `SharedWith` membership is the "attached to me" rule.
    private static IQueryable<CalendarEvent> VisibleQuery(CalendarDbContext db, ICurrentMember me)
    {
        var q = db.Events.AsNoTracking().Where(e => e.HouseholdId == me.HouseholdId);
        return me.IsManager
            ? q.Where(e => e.Visibility != Visibility.Private || e.OwnerId == me.Id)
            : q.Where(e => e.OwnerId == me.Id || e.Visibility == Visibility.Household || e.Visibility == Visibility.Shared);
    }

    // The full visibility predicate (evaluated in memory so the JSON SharedWith list can be checked).
    private static bool CanSee(CalendarEvent e, ICurrentMember me) =>
        e.OwnerId == me.Id
        || e.Visibility == Visibility.Household
        || (me.IsManager && e.Visibility != Visibility.Private)
        || (e.Visibility == Visibility.Shared && e.SharedWith.Contains(me.Id));

    private static async Task<CalendarEvent?> Editable(CalendarDbContext db, ICurrentMember me, Guid id, CancellationToken ct)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == id && e.HouseholdId == me.HouseholdId, ct);
        if (ev is null) return null;
        if (me.IsManager && ev.Visibility != Visibility.Private) return ev;
        return ev.OwnerId == me.Id ? ev : null;
    }

    private static EventDto ToDto(CalendarEvent e, ICurrentMember me) => new(
        e.Id, e.Title, e.StartsOn.ToString("yyyy-MM-dd"), e.StartTime?.ToString("HH:mm"),
        e.Location, e.Notes, e.Visibility.ToString(), e.OwnerId,
        (me.IsManager && e.Visibility != Visibility.Private) || e.OwnerId == me.Id,
        e.SharedWith);

    private static DateOnly? ParseDate(string? v) => DateOnly.TryParse(v, out var d) ? d : null;
    private static TimeOnly? ParseTime(string? v) => TimeOnly.TryParse(v, CultureInfo.InvariantCulture, out var t) ? t : null;
    private static Visibility ParseVisibility(string? v) => Enum.TryParse<Visibility>(v, true, out var x) ? x : Visibility.Household;
}

using System.Globalization;
using HomeOs.Modules.LifeAdmin.Domain;
using HomeOs.Modules.LifeAdmin.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Reminders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.LifeAdmin.Features;

/// <summary>Life admin — documents/warranties/renewals; an expiry date drives an automatic reminder.</summary>
public static class LifeAdminEndpoints
{
    private const string Source = "lifeadmin";
    private const int LeadDays = 7; // remind this many days before expiry

    public static IEndpointRouteBuilder MapLifeAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/life").RequireAuthorization().WithTags("LifeAdmin");

        group.MapGet("/", ListAsync).WithName("ListLifeRecords");
        group.MapPost("/", CreateAsync).WithName("CreateLifeRecord");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateLifeRecord");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteLifeRecord");

        return app;
    }

    private static async Task<IResult> ListAsync(ICurrentMember me, LifeAdminDbContext db, CancellationToken ct)
    {
        var records = await VisibleTo(db, me)
            .OrderBy(r => r.ExpiresOn == null).ThenBy(r => r.ExpiresOn).ThenBy(r => r.Title)
            .ToListAsync(ct);
        return Results.Ok(records.Select(r => ToDto(r, me)).ToList());
    }

    private static async Task<IResult> CreateAsync(
        SaveLifeRecordRequest req, ICurrentMember me, LifeAdminDbContext db,
        IReminderService reminders, IAppText text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.life.required"]] });

        var record = LifeRecord.Create(me.HouseholdId, me.Id, req.Title, ParseCategory(req.Category),
            ParseDate(req.ExpiresOn), req.Provider, req.Notes, ParseVisibility(req.Visibility));

        db.Records.Add(record);
        await db.SaveChangesAsync(ct);
        await SyncReminderAsync(record, reminders, text, ct); // ← the cross-module automation
        return Results.Created($"/api/life/{record.Id}", ToDto(record, me));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, SaveLifeRecordRequest req, ICurrentMember me, LifeAdminDbContext db,
        IReminderService reminders, IAppText text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.life.required"]] });

        var record = await Editable(db, me, id, ct);
        if (record is null) return Results.NotFound();

        record.Update(req.Title, ParseCategory(req.Category), ParseDate(req.ExpiresOn), req.Provider, req.Notes, ParseVisibility(req.Visibility));
        await db.SaveChangesAsync(ct);
        await SyncReminderAsync(record, reminders, text, ct);
        return Results.Ok(ToDto(record, me));
    }

    private static async Task<IResult> DeleteAsync(Guid id, ICurrentMember me, LifeAdminDbContext db, IReminderService reminders, CancellationToken ct)
    {
        var record = await Editable(db, me, id, ct);
        if (record is null) return Results.NotFound();
        db.Records.Remove(record);
        await db.SaveChangesAsync(ct);
        await reminders.RemoveAsync(record.HouseholdId, Source, record.Id, ct); // clean up its reminder
        return Results.NoContent();
    }

    // Schedules (or removes) the reminder for a record's expiry — the "renewal → reminder" automation.
    private static async Task SyncReminderAsync(LifeRecord record, IReminderService reminders, IAppText text, CancellationToken ct)
    {
        var today = Today();
        if (record.ExpiresOn is { } expiry && expiry >= today)
        {
            var remindOn = expiry.AddDays(-LeadDays);
            if (remindOn < today) remindOn = today;
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var title = text.T(lang, "reminder.expiry", record.Title);
            await reminders.ScheduleAsync(new ScheduledReminder(
                record.HouseholdId, record.OwnerId, record.OwnerId, title, remindOn, null, Source, record.Id), ct);
        }
        else
        {
            await reminders.RemoveAsync(record.HouseholdId, Source, record.Id, ct);
        }
    }

    // ---- helpers ----
    private static IQueryable<LifeRecord> VisibleTo(LifeAdminDbContext db, ICurrentMember me)
    {
        var q = db.Records.AsNoTracking().Where(r => r.HouseholdId == me.HouseholdId);
        return me.IsManager
            ? q.Where(r => r.Visibility != Visibility.Private || r.OwnerId == me.Id)
            : q.Where(r => r.OwnerId == me.Id || r.Visibility == Visibility.Household);
    }

    private static async Task<LifeRecord?> Editable(LifeAdminDbContext db, ICurrentMember me, Guid id, CancellationToken ct)
    {
        var r = await db.Records.FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == me.HouseholdId, ct);
        if (r is null) return null;
        if (me.IsManager && r.Visibility != Visibility.Private) return r;
        return r.OwnerId == me.Id ? r : null;
    }

    private static LifeRecordDto ToDto(LifeRecord r, ICurrentMember me) => new(
        r.Id, r.Title, r.Category.ToString(), r.ExpiresOn?.ToString("yyyy-MM-dd"),
        r.ExpiresOn is { } e ? e.DayNumber - Today().DayNumber : null,
        r.Provider, r.Notes, r.Visibility.ToString(), r.OwnerId,
        (me.IsManager && r.Visibility != Visibility.Private) || r.OwnerId == me.Id);

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
    private static DateOnly? ParseDate(string? v) => DateOnly.TryParse(v, out var d) ? d : null;
    private static LifeCategory ParseCategory(string? v) => Enum.TryParse<LifeCategory>(v, true, out var c) ? c : LifeCategory.Document;
    private static Visibility ParseVisibility(string? v) => Enum.TryParse<Visibility>(v, true, out var x) ? x : Visibility.Household;
}

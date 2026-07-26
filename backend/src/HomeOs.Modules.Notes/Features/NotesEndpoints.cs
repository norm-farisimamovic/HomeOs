using HomeOs.Modules.Notes.Contracts;
using HomeOs.Modules.Notes.Domain;
using HomeOs.Modules.Notes.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Events;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Notes.Features;

/// <summary>Notes CRUD — household-scoped, member-visible (incl. shared-with-me), event-publishing.</summary>
public static class NotesEndpoints
{
    public static IEndpointRouteBuilder MapNotesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notes").RequireAuthorization().WithTags("Notes");

        group.MapGet("/", ListAsync).WithName("ListNotes");
        group.MapPost("/", CreateAsync).WithName("CreateNote");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateNote");
        group.MapPost("/{id:guid}/pin", PinAsync).WithName("PinNote");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteNote");

        return app;
    }

    private static async Task<IResult> ListAsync(ICurrentMember me, NotesDbContext db, CancellationToken ct)
    {
        var notes = (await VisibleQuery(db, me)
                .OrderByDescending(n => n.Pinned).ThenByDescending(n => n.UpdatedAtUtc)
                .ToListAsync(ct))
            .Where(n => CanSee(n, me))
            .Select(n => ToDto(n, me))
            .ToList();
        return Results.Ok(notes);
    }

    private static async Task<IResult> CreateAsync(
        SaveNoteRequest req, ICurrentMember me, NotesDbContext db, IEventBus bus, IShareNotifier share, IAppText text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.note.required"]] });

        var note = Note.Create(me.HouseholdId, me.Id, req.Title, req.Content, req.Tags, ParseVisibility(req.Visibility), req.SharedWith, ParseEntryDate(req.EntryDate));
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new NoteCreated(note.Id, note.HouseholdId, note.Title), ct);
        await share.NotifySharedAsync(note.HouseholdId, me.Id, note.SharedWith, note.Title, "/notes", ct);
        return Results.Created($"/api/notes/{note.Id}", ToDto(note, me));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, SaveNoteRequest req, ICurrentMember me, NotesDbContext db, IShareNotifier share, IAppText text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.note.required"]] });

        var note = await Editable(db, me, id, ct);
        if (note is null) return Results.NotFound();

        var before = note.SharedWith.ToHashSet();
        note.Update(req.Title, req.Content, req.Tags, ParseVisibility(req.Visibility), req.SharedWith, ParseEntryDate(req.EntryDate));
        await db.SaveChangesAsync(ct);
        await share.NotifySharedAsync(note.HouseholdId, me.Id, note.SharedWith.Where(x => !before.Contains(x)), note.Title, "/notes", ct);
        return Results.Ok(ToDto(note, me));
    }

    private static async Task<IResult> PinAsync(Guid id, PinNoteRequest req, ICurrentMember me, NotesDbContext db, CancellationToken ct)
    {
        var note = await Editable(db, me, id, ct);
        if (note is null) return Results.NotFound();
        note.SetPinned(req.Pinned);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(note, me));
    }

    private static async Task<IResult> DeleteAsync(Guid id, ICurrentMember me, NotesDbContext db, CancellationToken ct)
    {
        var note = await Editable(db, me, id, ct);
        if (note is null) return Results.NotFound();
        db.Notes.Remove(note);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ---- helpers (translatable coarse filter + in-memory refine, since Pomelo can't query JSON SharedWith) ----
    private static IQueryable<Note> VisibleQuery(NotesDbContext db, ICurrentMember me)
    {
        var q = db.Notes.AsNoTracking().Where(n => n.HouseholdId == me.HouseholdId);
        return me.IsManager
            ? q.Where(n => n.Visibility != Visibility.Private || n.OwnerId == me.Id)
            : q.Where(n => n.OwnerId == me.Id || n.Visibility == Visibility.Household || n.Visibility == Visibility.Shared);
    }

    private static bool CanSee(Note n, ICurrentMember me) =>
        n.OwnerId == me.Id
        || n.Visibility == Visibility.Household
        || (me.IsManager && n.Visibility != Visibility.Private)
        || (n.Visibility == Visibility.Shared && n.SharedWith.Contains(me.Id));

    // Editable/deletable = the author or a manager on a non-private note.
    private static async Task<Note?> Editable(NotesDbContext db, ICurrentMember me, Guid id, CancellationToken ct)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.HouseholdId == me.HouseholdId, ct);
        if (note is null) return null;
        if (me.IsManager && note.Visibility != Visibility.Private) return note;
        return note.OwnerId == me.Id ? note : null;
    }

    private static NoteDto ToDto(Note n, ICurrentMember me) => new(
        n.Id, n.Title, n.Content, n.Tags, n.Pinned, n.Visibility.ToString(), n.SharedWith,
        n.EntryDate?.ToString("yyyy-MM-dd"), n.UpdatedAtUtc.ToString("o"), n.OwnerId,
        (me.IsManager && n.Visibility != Visibility.Private) || n.OwnerId == me.Id);

    private static Visibility ParseVisibility(string? v) => Enum.TryParse<Visibility>(v, true, out var x) ? x : Visibility.Household;
    private static DateOnly? ParseEntryDate(string? v) => DateOnly.TryParse(v, out var d) ? d : null;
}

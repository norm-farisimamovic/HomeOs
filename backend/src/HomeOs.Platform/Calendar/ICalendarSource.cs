namespace HomeOs.Platform.Calendar;

/// <summary>
/// A dated item contributed to the shared calendar by any app — a task's due date, a bill's next-due,
/// a calendar event. This is how "everything connects" without apps referencing each other: each module
/// exposes its dated items as <see cref="CalendarItem"/>s, and the Calendar app merges them.
/// </summary>
/// <param name="Source">Owning app key: <c>tasks</c>, <c>finance</c>, <c>calendar</c> — the UI colours by this.</param>
/// <param name="Id">The underlying object's id (so the UI can deep-link back to it).</param>
/// <param name="Title">Human label to show on the day.</param>
/// <param name="Date">The day it lands on.</param>
/// <param name="Kind">Object kind: <c>task</c>, <c>bill</c>, <c>event</c>.</param>
/// <param name="Time">Optional time (<c>HH:mm</c>) for timed items; null for all-day.</param>
/// <param name="IsDone">Completed/settled items render muted.</param>
public sealed record CalendarItem(
    string Source,
    Guid Id,
    string Title,
    DateOnly Date,
    string Kind,
    string? Time = null,
    bool IsDone = false);

/// <summary>
/// Implemented by any module with dated items worth showing on the calendar. Registered as a scoped
/// service; the source resolves the current member itself (via <c>ICurrentMember</c>) and returns only
/// items that member may see. The Calendar app injects <c>IEnumerable&lt;ICalendarSource&gt;</c> and merges.
/// </summary>
public interface ICalendarSource
{
    /// <summary>Items visible to the current member landing within <paramref name="from"/>..<paramref name="to"/> (inclusive).</summary>
    Task<IReadOnlyList<CalendarItem>> GetItemsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

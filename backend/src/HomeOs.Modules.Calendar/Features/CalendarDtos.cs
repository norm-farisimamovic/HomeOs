namespace HomeOs.Modules.Calendar.Features;

/// <summary>A calendar event as returned to clients (dates/times as strings).</summary>
public sealed record EventDto(
    Guid Id, string Title, string StartsOn, string? StartTime, string? Location, string? Notes,
    string Visibility, Guid OwnerId, bool CanEdit, IReadOnlyList<Guid> SharedWith);

/// <summary>One dated item on the month feed — from any app (event/task/bill), merged and colour-coded.</summary>
public sealed record CalendarFeedItem(string Source, Guid Id, string Title, string Date, string Kind, string? Time, bool IsDone);

/// <summary>The month feed: the requested month plus every source's items landing in it.</summary>
public sealed record MonthFeedDto(int Year, int Month, IReadOnlyList<CalendarFeedItem> Items);

/// <summary>Create/update payload for an event. <c>SharedWith</c> applies when Visibility is <c>Shared</c>.</summary>
public sealed record SaveEventRequest(
    string Title, string StartsOn, string? StartTime, string? Location, string? Notes, string? Visibility,
    IReadOnlyList<Guid>? SharedWith);

using HomeOs.Platform.Entities;

namespace HomeOs.Modules.Calendar.Domain;

/// <summary>A household calendar event — a single-day entry, optionally timed, with a place and notes.</summary>
public sealed class CalendarEvent : IHomeObject
{
    private CalendarEvent() { }

    public static CalendarEvent Create(Guid householdId, Guid ownerId, string title, DateOnly startsOn,
        TimeOnly? startTime, string? location, string? notes, Visibility visibility, IEnumerable<Guid>? sharedWith = null) => new()
    {
        HouseholdId = householdId,
        OwnerId = ownerId,
        Title = title.Trim(),
        StartsOn = startsOn,
        StartTime = startTime,
        Location = location?.Trim(),
        Notes = notes?.Trim(),
        Visibility = visibility,
        SharedWith = visibility == Visibility.Shared ? (sharedWith ?? []).Distinct().ToList() : [],
    };

    /// <summary>Edits the mutable fields in place.</summary>
    public void Update(string title, DateOnly startsOn, TimeOnly? startTime, string? location, string? notes,
        Visibility visibility, IEnumerable<Guid>? sharedWith = null)
    {
        Title = title.Trim();
        StartsOn = startsOn;
        StartTime = startTime;
        Location = location?.Trim();
        Notes = notes?.Trim();
        Visibility = visibility;
        SharedWith = visibility == Visibility.Shared ? (sharedWith ?? []).Distinct().ToList() : [];
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ObjectType => "event";
    public Guid HouseholdId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateOnly StartsOn { get; private set; }
    public TimeOnly? StartTime { get; private set; }
    public string? Location { get; private set; }
    public string? Notes { get; private set; }
    public Visibility Visibility { get; private set; } = Visibility.Household;

    /// <summary>When <see cref="Visibility"/> is <c>Shared</c>, the member ids who may see it (plus the owner).</summary>
    public List<Guid> SharedWith { get; private set; } = [];

    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
}

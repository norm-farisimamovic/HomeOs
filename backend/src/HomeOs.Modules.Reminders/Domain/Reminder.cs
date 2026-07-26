using HomeOs.Platform.Entities;

namespace HomeOs.Modules.Reminders.Domain;

/// <summary>How often a reminder repeats. <see cref="None"/> is a one-off.</summary>
public enum Recurrence { None = 0, Daily = 1, Weekly = 2, Monthly = 3, Yearly = 4 }

/// <summary>A reminder aimed at a household member — a dated (optionally timed) nudge that can be ticked off.</summary>
public sealed class Reminder : IHomeObject
{
    private Reminder() { }

    public static Reminder Create(Guid householdId, Guid ownerId, Guid forMemberId, string title,
        DateOnly remindOn, TimeOnly? remindAt, string? notes, Visibility visibility, Recurrence recurrence = Recurrence.None) => new()
    {
        HouseholdId = householdId,
        OwnerId = ownerId,
        ForMemberId = forMemberId,
        Title = title.Trim(),
        RemindOn = remindOn,
        RemindAt = remindAt,
        Notes = notes?.Trim(),
        Visibility = visibility,
        Recurrence = recurrence,
    };

    /// <summary>Edits the mutable fields in place. Resets the alert ladder so a changed date fires afresh.</summary>
    public void Update(Guid forMemberId, string title, DateOnly remindOn, TimeOnly? remindAt, string? notes, Visibility visibility, Recurrence recurrence)
    {
        ForMemberId = forMemberId;
        Title = title.Trim();
        RemindOn = remindOn;
        RemindAt = remindAt;
        Notes = notes?.Trim();
        Visibility = visibility;
        Recurrence = recurrence;
        NotifiedAtUtc = null;
        NotifiedLeadDays = null;
    }

    /// <summary>A reminder created on behalf of another app's object (e.g. a warranty expiry), tied to its source.</summary>
    public static Reminder CreateForSource(Guid householdId, Guid ownerId, Guid forMemberId, string title,
        DateOnly remindOn, TimeOnly? remindAt, string sourceKey, Guid sourceId) => new()
    {
        HouseholdId = householdId,
        OwnerId = ownerId,
        ForMemberId = forMemberId,
        Title = title.Trim(),
        RemindOn = remindOn,
        RemindAt = remindAt,
        Visibility = Visibility.Household,
        SourceKey = sourceKey,
        SourceId = sourceId,
    };

    /// <summary>Re-points an existing source-linked reminder at a new title/date (keeps its done state reset).</summary>
    public void Reschedule(string title, DateOnly remindOn, TimeOnly? remindAt)
    {
        Title = title.Trim();
        RemindOn = remindOn;
        RemindAt = remindAt;
        IsDone = false;
        NotifiedAtUtc = null;    // a new date should fire again
        NotifiedLeadDays = null;
    }

    /// <summary>
    /// Ticks the reminder off. A one-off is marked done; a recurring one instead advances to its next
    /// occurrence after <paramref name="today"/> (resetting the alert ladder) and stays active.
    /// </summary>
    public void Complete(DateOnly today)
    {
        if (Recurrence == Recurrence.None)
        {
            IsDone = true;
            return;
        }

        var next = NextOccurrence(RemindOn, Recurrence);
        for (var guard = 0; next <= today && guard < 600; guard++)
            next = NextOccurrence(next, Recurrence);
        RemindOn = next;
        IsDone = false;
        NotifiedAtUtc = null;
        NotifiedLeadDays = null;
    }

    public void Reopen() => IsDone = false;

    private static DateOnly NextOccurrence(DateOnly date, Recurrence recurrence) => recurrence switch
    {
        Recurrence.Daily => date.AddDays(1),
        Recurrence.Weekly => date.AddDays(7),
        Recurrence.Monthly => date.AddMonths(1),
        Recurrence.Yearly => date.AddYears(1),
        _ => date,
    };

    /// <summary>Records that the alert for a given lead-day stage has been sent (see <c>LeadSchedule</c>).</summary>
    public void MarkNotified(int stage)
    {
        NotifiedLeadDays = stage;
        NotifiedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ObjectType => "reminder";
    public Guid HouseholdId { get; private set; }
    public Guid OwnerId { get; private set; }

    /// <summary>The member this reminder is for (they always see it — "attached to me").</summary>
    public Guid ForMemberId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public DateOnly RemindOn { get; private set; }
    public TimeOnly? RemindAt { get; private set; }
    public string? Notes { get; private set; }
    public Visibility Visibility { get; private set; } = Visibility.Private;

    /// <summary>How often the reminder repeats (<see cref="Recurrence.None"/> = one-off).</summary>
    public Recurrence Recurrence { get; private set; } = Recurrence.None;

    public bool IsDone { get; private set; }

    /// <summary>When this reminder was auto-created by another app, the owning app key (e.g. <c>lifeadmin</c>); else null.</summary>
    public string? SourceKey { get; private set; }

    /// <summary>The source object's id, so the reminder can be updated/removed with it.</summary>
    public Guid? SourceId { get; private set; }

    /// <summary>When the most recent alert was sent (null = never fired).</summary>
    public DateTimeOffset? NotifiedAtUtc { get; private set; }

    /// <summary>
    /// The lead-day stage last alerted at (days before <see cref="RemindOn"/>; 0 = the day itself). Null = no
    /// alert yet. Drives the escalating run-up ("in 3 / 1 day, then today") so each stage fires once.
    /// </summary>
    public int? NotifiedLeadDays { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
}

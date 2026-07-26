namespace HomeOs.Modules.Reminders.Features;

/// <summary>A reminder as returned to clients (dates/times as strings; target enriched with a name).</summary>
public sealed record ReminderDto(
    Guid Id, string Title, string RemindOn, string? RemindAt, string? Notes,
    Guid ForMemberId, string? ForMemberName, string Visibility, string Recurrence, bool IsDone, bool IsOverdue,
    Guid OwnerId, bool CanEdit);

/// <summary>Create/update payload for a reminder.</summary>
public sealed record SaveReminderRequest(
    string Title, string RemindOn, string? RemindAt, string? Notes, Guid? ForMemberId, string? Visibility, string? Recurrence);

using HomeOs.Platform.Events;

namespace HomeOs.Modules.Reminders.Contracts;

/// <summary>Raised after a reminder is created (other apps/notifications may react later).</summary>
public sealed record ReminderCreated(Guid ReminderId, Guid HouseholdId, Guid ForMemberId, string Title, DateOnly RemindOn) : IDomainEvent;

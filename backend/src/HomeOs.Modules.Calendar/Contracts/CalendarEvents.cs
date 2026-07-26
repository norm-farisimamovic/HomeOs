using HomeOs.Platform.Events;

namespace HomeOs.Modules.Calendar.Contracts;

/// <summary>Raised after a new calendar event is scheduled (other apps may react — e.g. reminders later).</summary>
public sealed record EventScheduled(Guid EventId, Guid HouseholdId, string Title, DateOnly StartsOn) : IDomainEvent;

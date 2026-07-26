using HomeOs.Platform.Events;

namespace HomeOs.Modules.Tasks.Contracts;

// Public contracts — other apps (Calendar, Reminders, Notifications) subscribe to these.
// Change only additively.

/// <summary>Raised after a task is created.</summary>
public sealed record TaskCreated(Guid TaskId, Guid HouseholdId, Guid OwnerId, Guid? AssigneeId, DateOnly? DueDate, string Title) : IDomainEvent;

/// <summary>Raised after a task is edited.</summary>
public sealed record TaskUpdated(Guid TaskId, Guid HouseholdId) : IDomainEvent;

/// <summary>Raised after a task is completed. <paramref name="CompletedById"/> is who ticked it off.</summary>
public sealed record TaskCompleted(Guid TaskId, Guid HouseholdId, Guid? AssigneeId, string Title, Guid CompletedById) : IDomainEvent;

/// <summary>Raised after a completed task is re-opened (un-ticked).</summary>
public sealed record TaskReopened(Guid TaskId, Guid HouseholdId) : IDomainEvent;

/// <summary>Raised after a task is deleted.</summary>
public sealed record TaskDeleted(Guid TaskId, Guid HouseholdId) : IDomainEvent;

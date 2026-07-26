using HomeOs.Platform.Events;

namespace HomeOs.Modules.Notes.Contracts;

/// <summary>Raised after a note is created (for the connected web / search indexing later).</summary>
public sealed record NoteCreated(Guid NoteId, Guid HouseholdId, string Title) : IDomainEvent;

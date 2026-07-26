namespace HomeOs.Modules.Notes.Features;

/// <summary>A note as returned to clients. <c>EntryDate</c> (non-null) marks a journal entry.</summary>
public sealed record NoteDto(
    Guid Id, string Title, string Content, IReadOnlyList<string> Tags, bool Pinned,
    string Visibility, IReadOnlyList<System.Guid> SharedWith, string? EntryDate, string UpdatedAt,
    System.Guid OwnerId, bool CanEdit);

/// <summary>Create/update payload for a note. <c>SharedWith</c> applies when Visibility is <c>Shared</c>; <c>EntryDate</c> makes it a journal entry.</summary>
public sealed record SaveNoteRequest(
    string Title, string? Content, IReadOnlyList<string>? Tags, string? Visibility, IReadOnlyList<System.Guid>? SharedWith, string? EntryDate);

/// <summary>Pin/unpin payload.</summary>
public sealed record PinNoteRequest(bool Pinned);

using HomeOs.Platform.Entities;

namespace HomeOs.Modules.Notes.Domain;

/// <summary>A household note — a title + free text, optionally tagged, pinned, and shared with members.</summary>
public sealed class Note : IHomeObject
{
    private Note() { }

    public static Note Create(Guid householdId, Guid ownerId, string title, string? content,
        IEnumerable<string>? tags, Visibility visibility, IEnumerable<Guid>? sharedWith = null, DateOnly? entryDate = null) => new()
    {
        HouseholdId = householdId,
        OwnerId = ownerId,
        Title = title.Trim(),
        Content = content?.Trim() ?? string.Empty,
        Tags = CleanTags(tags),
        Visibility = visibility,
        SharedWith = visibility == Visibility.Shared ? (sharedWith ?? []).Distinct().ToList() : [],
        EntryDate = entryDate,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    /// <summary>Edits the mutable fields in place and bumps the updated timestamp.</summary>
    public void Update(string title, string? content, IEnumerable<string>? tags, Visibility visibility, IEnumerable<Guid>? sharedWith, DateOnly? entryDate = null)
    {
        Title = title.Trim();
        Content = content?.Trim() ?? string.Empty;
        Tags = CleanTags(tags);
        Visibility = visibility;
        SharedWith = visibility == Visibility.Shared ? (sharedWith ?? []).Distinct().ToList() : [];
        EntryDate = entryDate;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetPinned(bool pinned) { Pinned = pinned; UpdatedAtUtc = DateTimeOffset.UtcNow; }

    private static List<string> CleanTags(IEnumerable<string>? tags) =>
        (tags ?? []).Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().Take(12).ToList();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string ObjectType => "note";
    public Guid HouseholdId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public List<string> Tags { get; private set; } = [];
    public bool Pinned { get; private set; }
    public Visibility Visibility { get; private set; } = Visibility.Household;

    /// <summary>When <see cref="Visibility"/> is <c>Shared</c>, the member ids who may see it (plus the owner).</summary>
    public List<Guid> SharedWith { get; private set; } = [];

    /// <summary>When set, the note is a dated journal entry (drives the journal view); null = a plain note.</summary>
    public DateOnly? EntryDate { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
}

namespace HomeOs.Platform.Notifications;

/// <summary>An in-app notification for a single member (the bell feed). Emailing is a separate, opt-in step.</summary>
public sealed class Notification
{
    private Notification() { }

    public static Notification For(Guid householdId, Guid memberId, string category, string title, string? body, string? link) => new()
    {
        HouseholdId = householdId,
        MemberId = memberId,
        Category = category,
        Title = title,
        Body = body,
        Link = link,
    };

    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Owning household (tenancy boundary).</summary>
    public Guid HouseholdId { get; private set; }

    /// <summary>Recipient member.</summary>
    public Guid MemberId { get; private set; }

    /// <summary>Category key (e.g. <c>reminder</c>, <c>taskAssigned</c>, <c>billDue</c>) — drives icon + email prefs.</summary>
    public string Category { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;
    public string? Body { get; private set; }

    /// <summary>In-app route to open (e.g. <c>/reminders</c>).</summary>
    public string? Link { get; private set; }

    public bool IsRead { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    public void MarkRead() => IsRead = true;
}

/// <summary>Per-member, per-category switch for whether a notification also goes out by email.</summary>
public sealed class NotificationPreference
{
    private NotificationPreference() { }

    public static NotificationPreference Create(Guid memberId, string category, bool email) =>
        new() { MemberId = memberId, Category = category, EmailEnabled = email };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid MemberId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public bool EmailEnabled { get; set; } = true;
}

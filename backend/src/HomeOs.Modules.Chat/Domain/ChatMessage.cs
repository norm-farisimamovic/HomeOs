namespace HomeOs.Modules.Chat.Domain;

/// <summary>A message in the household chat.</summary>
public sealed class ChatMessage
{
    private ChatMessage() { }

    public static ChatMessage Create(Guid householdId, Guid senderId, string text) => new()
    {
        HouseholdId = householdId,
        SenderId = senderId,
        Text = text.Trim(),
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid HouseholdId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; private set; } = DateTimeOffset.UtcNow;
}

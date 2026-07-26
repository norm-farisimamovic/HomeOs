namespace HomeOs.Modules.Automations.Domain;

/// <summary>
/// A user rule: "when {Trigger} happens, do {Action}". Triggers match <c>AppActivity.Kind</c> values;
/// the only action today is <c>notify</c> (an in-app notification, with an optional custom message).
/// </summary>
public sealed class Automation
{
    private Automation() { }

    public static Automation Create(Guid householdId, Guid ownerId, string name, string trigger, string action, string? message) => new()
    {
        HouseholdId = householdId,
        OwnerId = ownerId,
        Name = name.Trim(),
        Trigger = trigger.Trim(),
        Action = action.Trim(),
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
        Enabled = true,
    };

    public void Update(string name, string trigger, string action, string? message, bool enabled)
    {
        Name = name.Trim();
        Trigger = trigger.Trim();
        Action = action.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        Enabled = enabled;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid HouseholdId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Trigger { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string? Message { get; private set; }
    public bool Enabled { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
}

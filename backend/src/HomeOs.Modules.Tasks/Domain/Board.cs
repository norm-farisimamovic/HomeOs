namespace HomeOs.Modules.Tasks.Domain;

/// <summary>A named Kanban board for a household area (e.g. "Home", "Renovation"). Tasks belong to at most one.</summary>
public sealed class Board
{
    private Board() { }

    /// <summary>Creates a board with a display name and accent colour.</summary>
    public static Board Create(Guid householdId, string name, string color) => new()
    {
        HouseholdId = householdId,
        Name = name.Trim(),
        Color = string.IsNullOrWhiteSpace(color) ? "var(--m-boards)" : color.Trim(),
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = "var(--m-boards)";
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Renames the board / changes its colour.</summary>
    public void Update(string name, string color)
    {
        Name = name.Trim();
        Color = string.IsNullOrWhiteSpace(color) ? Color : color.Trim();
    }
}

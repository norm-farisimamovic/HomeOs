namespace HomeOs.Modules.Shopping.Domain;

/// <summary>A shared household list (shopping, to-buy, packing…) with checkable items.</summary>
public sealed class ShoppingList
{
    private ShoppingList() { }

    public static ShoppingList Create(Guid householdId, string name) => new()
    {
        HouseholdId = householdId,
        Name = name.Trim(),
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid HouseholdId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    public List<ShoppingItem> Items { get; private set; } = [];

    /// <summary>Renames the list.</summary>
    public void Rename(string name) => Name = name.Trim();
}

/// <summary>One checkable line on a <see cref="ShoppingList"/>.</summary>
public sealed class ShoppingItem
{
    private ShoppingItem() { }

    public static ShoppingItem Create(Guid listId, string text) => new()
    {
        ListId = listId,
        Text = text.Trim(),
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ListId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool Done { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Ticks the item off / back on.</summary>
    public void Toggle() => Done = !Done;
}

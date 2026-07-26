namespace HomeOs.Platform.Members;

/// <summary>A household — the top-level tenant that owns all data and groups its members.</summary>
public sealed class Household
{
    /// <summary>EF materialization constructor.</summary>
    private Household() { }

    /// <summary>Creates a new household with the given display name.</summary>
    public Household(string name) => Name = name;

    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Human-friendly household name (e.g. "The Imamović Home").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>When the household was created (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Members belonging to this household.</summary>
    public ICollection<Member> Members { get; private set; } = new List<Member>();

    /// <summary>Renames the household.</summary>
    public void Rename(string name) => Name = name;
}

namespace HomeOs.Platform.Apps;

/// <summary>
/// Per-household state for one app: whether it's enabled ("installed") and which capabilities the household
/// has granted it. Absence of a row means "default" (enabled with all the manifest's capabilities) so a new
/// app works immediately; the household changes that here to review, restrict, or remove it.
/// </summary>
public sealed class HouseholdApp
{
    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Owning household.</summary>
    public Guid HouseholdId { get; private set; }

    /// <summary>The app's manifest id.</summary>
    public string AppId { get; private set; } = string.Empty;

    /// <summary>Whether the app is installed/enabled for this household.</summary>
    public bool Enabled { get; private set; } = true;

    /// <summary>JSON array of granted capability strings.</summary>
    public string GrantedCapabilities { get; private set; } = "[]";

    /// <summary>When the row was first created.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>When the row was last changed.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private HouseholdApp() { }

    /// <summary>Creates a household-app row with the given enablement and granted capabilities (JSON).</summary>
    public static HouseholdApp Create(Guid householdId, string appId, bool enabled, string grantedCapabilitiesJson) => new()
    {
        HouseholdId = householdId,
        AppId = appId,
        Enabled = enabled,
        GrantedCapabilities = grantedCapabilitiesJson,
    };

    /// <summary>Sets enablement and stamps the change.</summary>
    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Replaces the granted-capabilities JSON and stamps the change.</summary>
    public void SetCapabilities(string grantedCapabilitiesJson)
    {
        GrantedCapabilities = grantedCapabilitiesJson;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

namespace HomeOs.Platform.Access;

/// <summary>
/// Household member roles (RBAC). Seeded as Identity roles and, from a later M1 slice, mapped to
/// permissions behind policies — code should check permissions/policies, not these strings directly.
/// </summary>
public static class HouseholdRoles
{
    /// <summary>Full control, including members and settings.</summary>
    public const string Owner = "Owner";

    /// <summary>Manages content and most settings.</summary>
    public const string Admin = "Admin";

    /// <summary>Normal member.</summary>
    public const string Adult = "Adult";

    /// <summary>Limited / curated access.</summary>
    public const string Child = "Child";

    /// <summary>Scoped / read-only access.</summary>
    public const string Guest = "Guest";

    /// <summary>All roles, in descending privilege order.</summary>
    public static readonly IReadOnlyList<string> All = [Owner, Admin, Adult, Child, Guest];
}

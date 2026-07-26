using Microsoft.AspNetCore.Identity;

namespace HomeOs.Platform.Members;

/// <summary>How often a member gets the "what's coming up" digest email.</summary>
public enum DigestFrequency { Off = 0, Daily = 1, Weekly = 2 }

/// <summary>
/// An authenticated household member — the identity/user of Home OS. Extends ASP.NET Core Identity's
/// user (with a <see cref="Guid"/> key) with household membership, a display name and a preferred culture.
/// </summary>
public sealed class Member : IdentityUser<Guid>
{
    /// <summary>Given (first) name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Family (last) name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Name shown across the app — kept as "First Last" so existing display sites need no change.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The household this member belongs to (tenancy boundary).</summary>
    public Guid HouseholdId { get; set; }

    /// <summary>
    /// Groups the accounts that belong to the same real person across households, so they can switch between
    /// their households without logging out. Each account is still a distinct login; only the primary one
    /// (the person's real email) is used to sign in — secondary households are reached via a trusted switch.
    /// </summary>
    public Guid PersonId { get; set; } = Guid.NewGuid();

    /// <summary>Navigation to the owning household.</summary>
    public Household? Household { get; set; }

    /// <summary>UI + notification language (e.g. "bs", "en").</summary>
    public string PreferredCulture { get; set; } = "bs";

    /// <summary>Currency the member sees money in (code, e.g. "BAM", "EUR"); Finance converts to it.</summary>
    public string PreferredCurrency { get; set; } = "BAM";

    /// <summary>Whether (and how often) the member gets the "what's coming up" digest email.</summary>
    public DigestFrequency DigestFrequency { get; set; } = DigestFrequency.Off;

    /// <summary>When the last digest was sent to this member (UTC); null = never.</summary>
    public DateTimeOffset? DigestLastSentUtc { get; set; }

    /// <summary>When the member was created (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Combines first + last into a display name (safe when either is missing).</summary>
    public static string FullName(string? first, string? last) =>
        string.Join(' ', new[] { first?.Trim(), last?.Trim() }.Where(s => !string.IsNullOrEmpty(s)));
}

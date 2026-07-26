using System.Security.Cryptography;

namespace HomeOs.Platform.Members;

/// <summary>A pending invitation for someone to join a household as a member with a given role.</summary>
public sealed class HouseholdInvite
{
    private HouseholdInvite() { } // EF

    /// <summary>Creates an invite valid for 14 days with a random URL-safe token.</summary>
    public static HouseholdInvite Create(Guid householdId, string email, string firstName, string lastName, string role, Guid invitedBy) => new()
    {
        HouseholdId = householdId,
        Email = email.Trim().ToLowerInvariant(),
        FirstName = firstName.Trim(),
        LastName = lastName.Trim(),
        DisplayName = Member.FullName(firstName, lastName),
        Role = role,
        InvitedByMemberId = invitedBy,
        Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),
        ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(14),
    };

    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Household the invite is for.</summary>
    public Guid HouseholdId { get; private set; }

    /// <summary>Invitee email (lower-cased).</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Invitee given name.</summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>Invitee family name.</summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>Invitee display name ("First Last").</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Role they'll get on acceptance.</summary>
    public string Role { get; private set; } = string.Empty;

    /// <summary>Opaque acceptance token (goes in the invite link).</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Who sent it.</summary>
    public Guid InvitedByMemberId { get; private set; }

    /// <summary>When it was created (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Expiry (UTC).</summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    /// <summary>When accepted (UTC), if it has been.</summary>
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    /// <summary>True while the invite can still be accepted.</summary>
    public bool IsPending => AcceptedAtUtc is null && ExpiresAtUtc > DateTimeOffset.UtcNow;

    /// <summary>Marks the invite accepted.</summary>
    public void Accept() => AcceptedAtUtc = DateTimeOffset.UtcNow;
}

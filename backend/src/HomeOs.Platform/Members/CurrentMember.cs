using System.Security.Claims;
using HomeOs.Platform.Access;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Members;

/// <summary>The authenticated caller — never trust the request body for identity or household.</summary>
public interface ICurrentMember
{
    /// <summary>Whether a member is authenticated on this request.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The member id (throws if unauthenticated).</summary>
    Guid Id { get; }

    /// <summary>The member's household id (throws if unauthenticated).</summary>
    Guid HouseholdId { get; }

    /// <summary>True for Owner/Admin — they see the whole household; others see only their own + shared.</summary>
    bool IsManager { get; }

    /// <summary>The member's preferred display currency code (e.g. "BAM", "EUR"). Defaults to BAM.</summary>
    string PreferredCurrency { get; }
}

/// <summary>
/// Resolves the current member from the auth cookie's id claim, looking up the household once per request.
/// </summary>
public sealed class CurrentMember(IHttpContextAccessor accessor, PlatformDbContext db) : ICurrentMember
{
    private Member? _member;
    private bool _loaded;

    private Guid? UserId
    {
        get
        {
            var raw = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    private Member? Member
    {
        get
        {
            if (_loaded) return _member;
            _loaded = true;
            if (UserId is { } id)
                _member = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == id);
            return _member;
        }
    }

    /// <inheritdoc />
    public bool IsAuthenticated => Member is not null;

    /// <inheritdoc />
    public bool IsManager =>
        accessor.HttpContext?.User is { } user &&
        (user.IsInRole(HouseholdRoles.Owner) || user.IsInRole(HouseholdRoles.Admin));

    /// <inheritdoc />
    public Guid Id => Member?.Id ?? throw new InvalidOperationException("No authenticated member.");

    /// <inheritdoc />
    public Guid HouseholdId => Member?.HouseholdId ?? throw new InvalidOperationException("No authenticated member.");

    /// <inheritdoc />
    public string PreferredCurrency => string.IsNullOrWhiteSpace(Member?.PreferredCurrency) ? "BAM" : Member!.PreferredCurrency;
}

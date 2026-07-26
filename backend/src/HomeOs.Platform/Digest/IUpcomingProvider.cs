namespace HomeOs.Platform.Digest;

/// <summary>One thing coming up for a member — the unit a digest email is built from.</summary>
/// <param name="Source">The contributing app id (e.g. <c>tasks</c>), matching its manifest.</param>
/// <param name="Title">What it is.</param>
/// <param name="Date">When it's due/scheduled.</param>
/// <param name="Kind">A short type tag the email can label (e.g. <c>task</c>, <c>bill</c>, <c>reminder</c>).</param>
public sealed record UpcomingItem(string Source, string Title, DateOnly Date, string Kind);

/// <summary>
/// Contributes a member's upcoming items to the digest. It's the member-explicit sibling of
/// <c>ICalendarSource</c>: because a digest is built by a background job (no current request), the member is
/// passed in rather than resolved from the request. Any app with dated items registers one and it folds into
/// everyone's "what's coming up" summary with no other change — the same aggregation-via-contract shape.
/// </summary>
public interface IUpcomingProvider
{
    /// <summary>The member's items dated within [<paramref name="from"/>, <paramref name="to"/>], newest-visible-first is not required.</summary>
    Task<IReadOnlyList<UpcomingItem>> GetUpcomingAsync(
        Guid householdId, Guid memberId, DateOnly from, DateOnly to, CancellationToken ct = default);
}

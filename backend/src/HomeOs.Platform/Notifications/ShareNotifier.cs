using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;

namespace HomeOs.Platform.Notifications;

/// <summary>
/// Kernel capability: tell members that something was shared with them. Any app that shares an object with
/// specific people calls this after saving; each recipient gets an in-app + email "shared with you"
/// notification in their own language. The actor is never notified about their own share.
/// </summary>
public interface IShareNotifier
{
    /// <summary>Notifies each newly-shared member (excluding the actor) that <paramref name="itemTitle"/> was shared.</summary>
    Task NotifySharedAsync(Guid householdId, Guid actorId, IEnumerable<Guid> sharedWith,
        string itemTitle, string link, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ShareNotifier(INotificationService notifications, IMemberDirectory directory, IAppText text) : IShareNotifier
{
    /// <inheritdoc />
    public async Task NotifySharedAsync(Guid householdId, Guid actorId, IEnumerable<Guid> sharedWith,
        string itemTitle, string link, CancellationToken ct = default)
    {
        var targets = sharedWith.Distinct().Where(id => id != actorId).ToList();
        if (targets.Count == 0) return;

        var members = await directory.GetHouseholdMembersAsync(householdId, ct);
        var byId = members.ToDictionary(m => m.Id);
        var actorName = byId.TryGetValue(actorId, out var actor) ? actor.DisplayName : string.Empty;

        foreach (var id in targets)
        {
            if (!byId.TryGetValue(id, out var member)) continue;
            var title = text.T(member.PreferredCulture, "shared.title", actorName, itemTitle);
            await notifications.NotifyAsync(householdId, id, "shared", title, null, link, alsoEmail: true, cancellationToken: ct);
        }
    }
}

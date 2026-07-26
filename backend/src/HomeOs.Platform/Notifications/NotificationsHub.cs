using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Notifications;

/// <summary>
/// Real-time channel for the signed-in member and their household. The default <c>IUserIdProvider</c> keys
/// connections by the NameIdentifier claim (= member id) for personal <c>notify</c> pushes; on connect we also
/// join a per-household group (<c>household:{id}</c>) so a data change by anyone is broadcast to everyone in the
/// home (the <c>changed</c> message → clients refetch). That's how a task ticked off on one screen updates the
/// dashboard on another.
/// </summary>
[Authorize]
public sealed class NotificationsHub(PlatformDbContext db) : Hub
{
    /// <summary>The SignalR group name for a household's live channel.</summary>
    public static string HouseholdGroup(Guid householdId) => $"household:{householdId}";

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.UserIdentifier, out var memberId))
        {
            var householdId = await db.Users.AsNoTracking()
                .Where(u => u.Id == memberId).Select(u => u.HouseholdId).FirstOrDefaultAsync();
            if (householdId != Guid.Empty)
                await Groups.AddToGroupAsync(Context.ConnectionId, HouseholdGroup(householdId));
        }
        await base.OnConnectedAsync();
    }
}

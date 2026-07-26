using System.Net;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HomeOs.Platform.Notifications;

/// <summary>
/// Kernel capability: raise a notification for a member. Always creates the in-app record and pushes it
/// live over SignalR; optionally also emails (respecting the member's per-category preference). Apps call
/// this instead of talking to email/SignalR directly.
/// </summary>
public interface INotificationService
{
    Task NotifyAsync(Guid householdId, Guid memberId, string category, string title,
        string? body = null, string? link = null, bool alsoEmail = false, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class NotificationService(
    PlatformDbContext db, IEmailSender email, IHubContext<NotificationsHub> hub, IAppText text, IConfiguration config)
    : INotificationService
{
    /// <inheritdoc />
    public async Task NotifyAsync(Guid householdId, Guid memberId, string category, string title,
        string? body = null, string? link = null, bool alsoEmail = false, CancellationToken cancellationToken = default)
    {
        var notification = Notification.For(householdId, memberId, category, title, body, link);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);

        // Live push to any open tabs of this member.
        await hub.Clients.User(memberId.ToString()).SendAsync("notify", new
        {
            id = notification.Id, category, title, body, link,
            createdAtUtc = notification.CreatedAtUtc,
        }, cancellationToken);

        if (!alsoEmail) return;

        // Only email if the member hasn't turned this category's email off.
        var pref = await db.NotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.MemberId == memberId && p.Category == category, cancellationToken);
        if (pref is { EmailEnabled: false }) return;

        var member = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == memberId, cancellationToken);
        if (member?.Email is not { Length: > 0 } address) return;
        // Don't send real mail to the demo/dev sink addresses (they'd just bounce).
        if (address.EndsWith("@homeos.local", StringComparison.OrdinalIgnoreCase)) return;

        var lang = member.PreferredCulture;
        var baseUrl = config["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
        var url = baseUrl + (link ?? "/");
        var html = text.EmailHtml(lang,
            text.T(lang, "email.notify.greeting", WebUtility.HtmlEncode(member.DisplayName)),
            WebUtility.HtmlEncode(body ?? title),
            text.T(lang, "email.notify.cta"), url, showRawLink: false);
        await email.SendAsync(new EmailMessage(address, title, html), cancellationToken);
    }
}

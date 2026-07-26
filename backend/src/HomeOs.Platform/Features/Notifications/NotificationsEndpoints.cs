using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Features.Notifications;

/// <summary>The bell feed + per-member email preferences.</summary>
public static class NotificationsEndpoints
{
    /// <summary>Categories a member can toggle email for (must match the frontend prefs UI).</summary>
    public static readonly string[] Categories = ["taskAssigned", "reminder", "billDue", "shared", "invite"];

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization().WithTags("Notifications");

        group.MapGet("/", ListAsync).WithName("ListNotifications");
        group.MapPost("/{id:guid}/read", MarkReadAsync).WithName("MarkNotificationRead");
        group.MapPost("/read-all", MarkAllReadAsync).WithName("MarkAllNotificationsRead");
        group.MapGet("/preferences", GetPrefsAsync).WithName("GetNotificationPrefs");
        group.MapPut("/preferences", SetPrefsAsync).WithName("SetNotificationPrefs");

        return app;
    }

    private static async Task<IResult> ListAsync(ICurrentMember me, PlatformDbContext db, CancellationToken ct)
    {
        var items = await db.Notifications.AsNoTracking()
            .Where(n => n.MemberId == me.Id)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(50)
            .Select(n => new NotificationDto(n.Id, n.Category, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAtUtc.ToString("o")))
            .ToListAsync(ct);
        var unread = await db.Notifications.CountAsync(n => n.MemberId == me.Id && !n.IsRead, ct);
        return Results.Ok(new NotificationsResponse(unread, items));
    }

    private static async Task<IResult> MarkReadAsync(Guid id, ICurrentMember me, PlatformDbContext db, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.MemberId == me.Id, ct);
        if (n is null) return Results.NotFound();
        n.MarkRead();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> MarkAllReadAsync(ICurrentMember me, PlatformDbContext db, CancellationToken ct)
    {
        await db.Notifications.Where(n => n.MemberId == me.Id && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetPrefsAsync(ICurrentMember me, PlatformDbContext db, CancellationToken ct)
    {
        var rows = await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.MemberId == me.Id).ToDictionaryAsync(p => p.Category, p => p.EmailEnabled, ct);
        // Default: email on unless the member turned it off.
        var prefs = Categories.Select(c => new NotificationPrefDto(c, rows.GetValueOrDefault(c, true))).ToList();
        return Results.Ok(prefs);
    }

    private static async Task<IResult> SetPrefsAsync(List<NotificationPrefDto> prefs, ICurrentMember me, PlatformDbContext db, CancellationToken ct)
    {
        foreach (var pref in prefs.Where(p => Categories.Contains(p.Category)))
        {
            var row = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.MemberId == me.Id && p.Category == pref.Category, ct);
            if (row is null) db.NotificationPreferences.Add(NotificationPreference.Create(me.Id, pref.Category, pref.Email));
            else row.EmailEnabled = pref.Email;
        }
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}

/// <summary>Feed response: unread count + recent items.</summary>
public sealed record NotificationsResponse(int Unread, IReadOnlyList<NotificationDto> Items);

/// <summary>A notification for the client.</summary>
public sealed record NotificationDto(Guid Id, string Category, string Title, string? Body, string? Link, bool IsRead, string CreatedAt);

/// <summary>A category email preference.</summary>
public sealed record NotificationPrefDto(string Category, bool Email);

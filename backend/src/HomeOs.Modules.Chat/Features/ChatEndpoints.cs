using System.Globalization;
using HomeOs.Modules.Chat.Domain;
using HomeOs.Modules.Chat.Persistence;
using HomeOs.Platform.Events;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;
using HomeOs.Platform.Reminders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Chat.Features;

/// <summary>Household chat — a shared message stream pushed live over SignalR, with @mentions and an @asistent bot.</summary>
public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat").RequireAuthorization().WithTags("Chat");

        group.MapGet("/", async (ICurrentMember me, ChatDbContext db, IMemberDirectory dir, CancellationToken ct) =>
        {
            var recent = await db.Messages.AsNoTracking()
                .Where(m => m.HouseholdId == me.HouseholdId)
                .OrderByDescending(m => m.SentAtUtc).Take(100).ToListAsync(ct);
            recent.Reverse();
            var names = await dir.GetNamesAsync(me.HouseholdId, ct);
            return Results.Ok(recent.Select(m => ToDto(m, names, me)).ToList());
        }).WithName("ListChat");

        group.MapPost("/", async (SendMessageRequest req, ICurrentMember me, ChatDbContext db, IMemberDirectory dir,
            IHubContext<NotificationsHub> hub, INotificationService notifications, IAppText text, IEventBus bus, CancellationToken ct) =>
        {
            var body = req.Text?.Trim();
            if (string.IsNullOrWhiteSpace(body)) return Results.BadRequest();
            if (body.Length > 2000) body = body[..2000];

            var members = await dir.GetHouseholdMembersAsync(me.HouseholdId, ct);
            var names = members.ToDictionary(m => m.Id, m => m.DisplayName);
            var senderName = names.GetValueOrDefault(me.Id, string.Empty);

            var message = ChatMessage.Create(me.HouseholdId, me.Id, body);
            db.Messages.Add(message);
            await db.SaveChangesAsync(ct);
            var dto = ToDto(message, names, me);
            await hub.Clients.Group(NotificationsHub.HouseholdGroup(me.HouseholdId)).SendAsync("chatMessage", dto, ct);

            // Announce on the event bus so a new message is a first-class automation trigger ("when a chat
            // message is posted, …") and part of the connected web — without being written to the audit log.
            await bus.PublishAsync(new AppActivity(me.HouseholdId, me.Id, "chat.message", body, "/chat"), ct);

            // @mentions → notify each mentioned member (not the sender). The AI lives on its own private
            // Assistant page, not here — the household chat is people only.
            foreach (var target in MentionedMembers(body, members).Where(m => m.Id != me.Id))
                await notifications.NotifyAsync(me.HouseholdId, target.Id, "mention",
                    text.T(target.PreferredCulture, "chat.mentionTitle", senderName), body, "/chat", alsoEmail: false, cancellationToken: ct);

            return Results.Ok(dto);
        }).WithName("SendChat");

        // Turn a message into a reminder for the current member (default: tomorrow).
        group.MapPost("/{id:guid}/reminder", async (Guid id, ToReminderRequest req, ICurrentMember me, ChatDbContext db, IReminderService reminders, CancellationToken ct) =>
        {
            var message = await db.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.HouseholdId == me.HouseholdId, ct);
            if (message is null) return Results.NotFound();
            var date = DateOnly.TryParse(req.Date, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
            var title = message.Text.Length > 120 ? message.Text[..120] : message.Text;
            await reminders.ScheduleAsync(new ScheduledReminder(me.HouseholdId, me.Id, me.Id, title, date, null, "chat", message.Id), ct);
            return Results.Ok(new { date = date.ToString("yyyy-MM-dd") });
        }).WithName("ChatToReminder");

        return app;
    }

    private static IEnumerable<MemberSummary> MentionedMembers(string text, IReadOnlyList<MemberSummary> members)
    {
        var lower = text.ToLower(CultureInfo.InvariantCulture);
        return members.Where(m =>
        {
            var first = (m.DisplayName.Split(' ').FirstOrDefault() ?? "").ToLower(CultureInfo.InvariantCulture);
            return first.Length > 0 && lower.Contains("@" + first);
        });
    }

    private static ChatMessageDto ToDto(ChatMessage m, IReadOnlyDictionary<Guid, string> names, ICurrentMember me) =>
        new(m.Id, m.SenderId, m.SenderId == Guid.Empty ? "Asistent" : names.GetValueOrDefault(m.SenderId), m.Text, m.SentAtUtc.ToString("o"), m.SenderId == me.Id);
}

/// <summary>A chat message for the client (<c>Mine</c> = sent by the current member; sender id <c>0…</c> = the assistant).</summary>
public sealed record ChatMessageDto(Guid Id, Guid SenderId, string? SenderName, string Text, string SentAt, bool Mine);

/// <summary>Send-message payload.</summary>
public sealed record SendMessageRequest(string Text);

/// <summary>Turn-into-reminder payload (optional date; defaults to tomorrow).</summary>
public sealed record ToReminderRequest(string? Date);

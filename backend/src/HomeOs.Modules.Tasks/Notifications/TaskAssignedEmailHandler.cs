using System.Globalization;
using HomeOs.Modules.Tasks.Contracts;
using HomeOs.Platform.Events;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;

namespace HomeOs.Modules.Tasks.Notifications;

/// <summary>
/// When a task is created with an assignee other than its creator, notifies the assignee — an in-app
/// notification (live via SignalR) plus an email if they haven't opted out. Demonstrates the platform
/// pattern: an app reacts to a domain event and uses the shared <see cref="INotificationService"/>.
/// </summary>
public sealed class TaskAssignedEmailHandler(IMemberDirectory members, INotificationService notifications, IAppText text)
    : IEventHandler<TaskAssigned>
{
    /// <inheritdoc />
    public async Task Handle(TaskAssigned domainEvent, CancellationToken cancellationToken)
    {
        // Don't notify the person who did the assigning (e.g. assigning a task to yourself).
        var assigneeId = domainEvent.AssigneeId;
        if (assigneeId == domainEvent.ActorId) return;

        var people = await members.GetHouseholdMembersAsync(domainEvent.HouseholdId, cancellationToken);
        var assignee = people.FirstOrDefault(m => m.Id == assigneeId);
        if (assignee is null) return;

        var lang = assignee.PreferredCulture;
        var body = text.T(lang, "email.task.line", domainEvent.Title);
        if (domainEvent.DueDate is { } d)
        {
            var dueStr = lang == "en" ? d.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : d.ToString("dd.MM.yyyy.");
            body += " " + text.T(lang, "email.task.due", dueStr);
        }

        await notifications.NotifyAsync(
            domainEvent.HouseholdId, assigneeId, "taskAssigned",
            text.T(lang, "email.task.subject", domainEvent.Title), body, "/tasks",
            alsoEmail: true, cancellationToken: cancellationToken);
    }
}

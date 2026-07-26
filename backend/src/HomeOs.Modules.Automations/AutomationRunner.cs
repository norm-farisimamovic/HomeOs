using HomeOs.Modules.Automations.Persistence;
using HomeOs.Platform.Events;
using HomeOs.Platform.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Automations;

/// <summary>
/// The automations engine. Subscribes to the single kernel <see cref="AppActivity"/> event, finds the
/// household's enabled rules whose trigger matches, and runs each rule's action. Coupled only to the
/// Platform kernel — it never references Tasks/Finance/Calendar/etc.
/// </summary>
public sealed class AutomationRunner(AutomationsDbContext db, INotificationService notifications)
    : IEventHandler<AppActivity>
{
    /// <inheritdoc />
    public async Task Handle(AppActivity domainEvent, CancellationToken cancellationToken)
    {
        var rules = await db.Automations.AsNoTracking()
            .Where(a => a.Enabled && a.HouseholdId == domainEvent.HouseholdId && a.Trigger == domainEvent.Kind)
            .ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            if (rule.Action != "notify") continue; // only action today; extend here
            var title = string.IsNullOrWhiteSpace(rule.Message) ? rule.Name : rule.Message!;
            await notifications.NotifyAsync(
                domainEvent.HouseholdId, rule.OwnerId, "automation",
                title, domainEvent.Title, domainEvent.Link, alsoEmail: false, cancellationToken: cancellationToken);
        }
    }
}

using HomeOs.Modules.Reminders.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;
using HomeOs.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeOs.Modules.Reminders.Dispatch;

/// <summary>
/// Fires reminders with an escalating run-up: as the date nears, the target member gets an in-app + email
/// nudge at each lead stage (3 days out, 1 day out, then on the day — see <see cref="LeadSchedule.Reminders"/>),
/// each firing once. Messages are written in the recipient's own language. A singleton hosted service that
/// opens a scope per tick to use the scoped DbContext + notification service.
/// </summary>
public sealed class ReminderDispatcher(IServiceScopeFactory scopeFactory, IAppText text, ILogger<ReminderDispatcher> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly int Widest = LeadSchedule.Reminders[0];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small startup delay so migrations/seeding finish first.
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try { await DispatchDueAsync(stoppingToken); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { logger.LogError(ex, "Reminder dispatch tick failed."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchDueAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(Widest);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RemindersDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var directory = scope.ServiceProvider.GetRequiredService<IMemberDirectory>();

        // Anything not done and already inside the widest lead window (or overdue); the ladder decides.
        var candidates = await db.Reminders
            .Where(r => !r.IsDone && r.RemindOn <= horizon)
            .OrderBy(r => r.RemindOn)
            .Take(200)
            .ToListAsync(ct);

        var cultures = new Dictionary<Guid, Dictionary<Guid, string>>();
        var sent = 0;

        foreach (var reminder in candidates)
        {
            var daysUntil = reminder.RemindOn.DayNumber - today.DayNumber;
            var stage = LeadSchedule.StageToFire(daysUntil, LeadSchedule.Reminders, reminder.NotifiedLeadDays);
            if (stage is null) continue;

            // Populate the household's member→culture cache once.
            await CultureFor(directory, cultures, reminder.HouseholdId, reminder.ForMemberId, ct);
            var memberCultures = cultures[reminder.HouseholdId];

            // A Household reminder nudges everyone in the home; a private one only its target member.
            var targets = reminder.Visibility == Visibility.Household
                ? memberCultures.Keys.ToList()
                : new List<Guid> { reminder.ForMemberId };

            foreach (var targetId in targets)
            {
                var culture = memberCultures.TryGetValue(targetId, out var c) && !string.IsNullOrWhiteSpace(c) ? c : "bs";
                // The lead-up stages carry an "in N days"/"tomorrow" prefix; the day-of alert is just the reminder.
                var lead = daysUntil <= 0 ? null
                    : daysUntil == 1 ? text.T(culture, "reminder.tomorrow")
                    : text.T(culture, "reminder.inDays", daysUntil);
                var body = string.Join("\n", new[] { lead, reminder.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)));

                await notifications.NotifyAsync(
                    reminder.HouseholdId, targetId, "reminder",
                    reminder.Title, string.IsNullOrEmpty(body) ? null : body, "/reminders", alsoEmail: true, cancellationToken: ct);
            }
            reminder.MarkNotified(stage.Value);
            sent++;
        }

        if (sent > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Dispatched {Count} reminder alert(s).", sent);
        }
    }

    // Resolve (and cache per household) the target member's preferred culture for localized messages.
    private static async Task<string> CultureFor(
        IMemberDirectory directory, Dictionary<Guid, Dictionary<Guid, string>> cache,
        Guid householdId, Guid memberId, CancellationToken ct)
    {
        if (!cache.TryGetValue(householdId, out var map))
        {
            var members = await directory.GetHouseholdMembersAsync(householdId, ct);
            map = members.ToDictionary(m => m.Id, m => m.PreferredCulture);
            cache[householdId] = map;
        }
        return map.TryGetValue(memberId, out var culture) && !string.IsNullOrWhiteSpace(culture) ? culture : "bs";
    }
}

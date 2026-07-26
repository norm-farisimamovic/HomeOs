using HomeOs.Modules.Finance.Persistence;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;
using HomeOs.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeOs.Modules.Finance.Dispatch;

/// <summary>
/// Escalating bill alerts: as a bill's due date approaches it raises an in-app + email notification at each
/// lead stage (a week out, then 3 days, 1 day, and on the day — see <see cref="LeadSchedule.Bills"/>), each
/// stage firing once. Messages are written in the recipient's own language.
/// </summary>
public sealed class BillDispatcher(IServiceScopeFactory scopeFactory, IAppText text, ILogger<BillDispatcher> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly int Widest = LeadSchedule.Bills[0];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try { await DispatchAsync(stoppingToken); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { logger.LogError(ex, "Bill alert tick failed."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(Widest);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var directory = scope.ServiceProvider.GetRequiredService<IMemberDirectory>();

        // Anything already inside the widest lead window (or overdue) is a candidate; the ladder decides.
        var candidates = await db.Bills
            .Where(b => b.NextDue <= horizon)
            .Take(200).ToListAsync(ct);

        var cultures = new Dictionary<Guid, Dictionary<Guid, string>>();
        var sent = 0;

        foreach (var bill in candidates)
        {
            var daysUntil = bill.NextDue.DayNumber - today.DayNumber;
            var stage = LeadSchedule.StageToFire(daysUntil, LeadSchedule.Bills, bill.NotifiedLeadDays);
            if (stage is null) continue;

            var target = bill.WhoPaysId ?? bill.OwnerId;
            var culture = await CultureFor(directory, cultures, bill.HouseholdId, target, ct);

            var title = daysUntil <= 0 ? text.T(culture, "bill.dueToday", bill.Name)
                : daysUntil == 1 ? text.T(culture, "bill.dueTomorrow", bill.Name)
                : text.T(culture, "bill.dueInDays", bill.Name, daysUntil);
            var body = $"{bill.Amount:0.##} {bill.Currency}";

            await notifications.NotifyAsync(bill.HouseholdId, target, "billDue", title, body, "/finance", alsoEmail: true, cancellationToken: ct);
            bill.MarkNotified(stage.Value);
            sent++;
        }

        // Roll recurring bills that have passed their due date onto the next cycle (fresh alert ladder).
        var rolled = 0;
        foreach (var bill in candidates)
            if (bill.IsRecurring && bill.NextDue < today)
            {
                bill.RollForwardTo(today);
                rolled++;
            }

        if (sent > 0 || rolled > 0)
        {
            await db.SaveChangesAsync(ct);
            if (sent > 0) logger.LogInformation("Sent {Count} bill-due alert(s).", sent);
            if (rolled > 0) logger.LogInformation("Rolled {Count} recurring bill(s) forward.", rolled);
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

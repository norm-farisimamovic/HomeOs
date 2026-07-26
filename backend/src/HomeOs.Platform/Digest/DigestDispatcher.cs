using HomeOs.Platform.Members;
using HomeOs.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeOs.Platform.Digest;

/// <summary>
/// Sends the opt-in "what's coming up" digest on each member's chosen cadence (daily or weekly). Runs hourly
/// and only emails members whose cadence is due since their last send, so it's safe to tick often.
/// </summary>
public sealed class DigestDispatcher(IServiceScopeFactory scopeFactory, ILogger<DigestDispatcher> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try { await RunAsync(stoppingToken); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { logger.LogError(ex, "Digest tick failed."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var digest = scope.ServiceProvider.GetRequiredService<IDigestService>();

        var subscribers = await db.Users.AsNoTracking()
            .Where(u => u.DigestFrequency != DigestFrequency.Off)
            .Select(u => new { u.Id, u.HouseholdId, u.DigestFrequency, u.DigestLastSentUtc })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var s in subscribers)
        {
            if (!IsDue(s.DigestFrequency, s.DigestLastSentUtc, now)) continue;
            var days = s.DigestFrequency == DigestFrequency.Daily ? 1 : 7;
            if (await digest.SendToMemberAsync(s.HouseholdId, s.Id, days, ct)) sent++;
        }

        if (sent > 0) logger.LogInformation("Sent {Count} digest email(s).", sent);
    }

    // Daily is due once a calendar day; weekly once every ~7 days. Never sent → always due.
    private static bool IsDue(DigestFrequency frequency, DateTimeOffset? lastSent, DateTimeOffset now)
    {
        if (lastSent is not { } last) return true;
        return frequency == DigestFrequency.Daily
            ? last.UtcDateTime.Date < now.UtcDateTime.Date
            : (now - last).TotalDays >= 6.5;
    }
}

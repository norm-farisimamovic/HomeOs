using HomeOs.Modules.LifeAdmin.Domain;
using HomeOs.Modules.LifeAdmin.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Reminders;
using HomeOs.Platform.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HomeOs.Modules.LifeAdmin.Seeding;

/// <summary>Dev-only: seeds a few life-admin records — one expiring soon — and their auto-reminders.</summary>
public sealed class LifeAdminDemoSeeder(
    IHostEnvironment env, IHouseholdLookup households, IMemberDirectory members,
    LifeAdminDbContext db, IReminderService reminders, IAppText text) : IDataSeeder
{
    public int Order => 160;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!env.IsDevelopment()) return;
        if (await households.FindHouseholdIdByNameAsync("Demo Home", cancellationToken) is not { } hid) return;
        if (await db.Records.AnyAsync(r => r.HouseholdId == hid, cancellationToken)) return;

        var people = await members.GetHouseholdMembersAsync(hid, cancellationToken);
        if (people.Count == 0) return;
        var ana = people[0].Id;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var records = new[]
        {
            LifeRecord.Create(hid, ana, "Lična karta", LifeCategory.Document, today.AddDays(40), "MUP", null, Visibility.Household),
            LifeRecord.Create(hid, ana, "Garancija za veš mašinu", LifeCategory.Warranty, today.AddDays(6), "Gorenje", "Račun u fascikli.", Visibility.Household),
            LifeRecord.Create(hid, ana, "Auto osiguranje", LifeCategory.Insurance, today.AddDays(25), "Sarajevo osiguranje", null, Visibility.Household),
        };
        db.Records.AddRange(records);
        await db.SaveChangesAsync(cancellationToken);

        // Demonstrate the automation in seed data too: each expiry schedules a reminder 7 days before.
        foreach (var r in records)
        {
            if (r.ExpiresOn is not { } exp) continue;
            var remindOn = exp.AddDays(-7);
            if (remindOn < today) remindOn = today;
            await reminders.ScheduleAsync(new ScheduledReminder(
                hid, ana, ana, text.T("bs", "reminder.expiry", r.Title), remindOn, null, "lifeadmin", r.Id), cancellationToken);
        }
    }
}

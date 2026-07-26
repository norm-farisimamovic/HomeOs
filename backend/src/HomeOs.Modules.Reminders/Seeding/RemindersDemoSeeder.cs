using HomeOs.Modules.Reminders.Domain;
using HomeOs.Modules.Reminders.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using HomeOs.Platform.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HomeOs.Modules.Reminders.Seeding;

/// <summary>Dev-only: seeds a few reminders into the demo household.</summary>
public sealed class RemindersDemoSeeder(
    IHostEnvironment env, IHouseholdLookup households, IMemberDirectory members, RemindersDbContext db) : IDataSeeder
{
    public int Order => 140;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!env.IsDevelopment()) return;
        if (await households.FindHouseholdIdByNameAsync("Demo Home", cancellationToken) is not { } hid) return;
        if (await db.Reminders.AnyAsync(r => r.HouseholdId == hid, cancellationToken)) return;

        var people = await members.GetHouseholdMembersAsync(hid, cancellationToken);
        if (people.Count == 0) return;
        var ana = people[0].Id;
        var mirza = people.Count > 1 ? people[1].Id : ana;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.Reminders.AddRange(
            Reminder.Create(hid, ana, ana, "Popiti lijek", today, new TimeOnly(8, 0), null, Visibility.Private),
            Reminder.Create(hid, ana, mirza, "Zaliti cvijeće", today.AddDays(1), new TimeOnly(19, 0), null, Visibility.Household),
            Reminder.Create(hid, ana, ana, "Nazvati majstora", today.AddDays(4), null, "Za bojler", Visibility.Household));

        await db.SaveChangesAsync(cancellationToken);
    }
}

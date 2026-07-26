using HomeOs.Modules.Calendar.Domain;
using HomeOs.Modules.Calendar.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using HomeOs.Platform.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HomeOs.Modules.Calendar.Seeding;

/// <summary>Dev-only: seeds a couple of events into the demo household so the calendar isn't empty.</summary>
public sealed class CalendarDemoSeeder(
    IHostEnvironment env, IConfiguration config, IHouseholdLookup households, IMemberDirectory members, CalendarDbContext db) : IDataSeeder
{
    public int Order => 130;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!DemoMode.IsEnabled(env, config)) return;
        if (await households.FindHouseholdIdByNameAsync("Demo Home", cancellationToken) is not { } hid) return;
        if (await db.Events.AnyAsync(e => e.HouseholdId == hid, cancellationToken)) return;

        var people = await members.GetHouseholdMembersAsync(hid, cancellationToken);
        if (people.Count == 0) return;
        var ana = people[0].Id;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.Events.AddRange(
            CalendarEvent.Create(hid, ana, "Roditeljski sastanak", today.AddDays(3), new TimeOnly(18, 0), "Škola", null, Visibility.Household),
            CalendarEvent.Create(hid, ana, "Ljekar — kontrola", today.AddDays(8), new TimeOnly(9, 30), "Dom zdravlja", null, Visibility.Household),
            CalendarEvent.Create(hid, ana, "Rođendan", today.AddDays(15), null, null, "Kupiti poklon", Visibility.Household));

        await db.SaveChangesAsync(cancellationToken);
    }
}

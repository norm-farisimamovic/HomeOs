using HomeOs.Modules.Automations.Domain;
using HomeOs.Modules.Automations.Persistence;
using HomeOs.Platform.Members;
using HomeOs.Platform.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HomeOs.Modules.Automations.Seeding;

/// <summary>Dev-only: seeds a couple of example automation rules into the demo household.</summary>
public sealed class AutomationsDemoSeeder(
    IHostEnvironment env, IHouseholdLookup households, IMemberDirectory members, AutomationsDbContext db) : IDataSeeder
{
    public int Order => 170;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!env.IsDevelopment()) return;
        if (await households.FindHouseholdIdByNameAsync("Demo Home", cancellationToken) is not { } hid) return;
        if (await db.Automations.AnyAsync(a => a.HouseholdId == hid, cancellationToken)) return;

        var people = await members.GetHouseholdMembersAsync(hid, cancellationToken);
        if (people.Count == 0) return;
        var ana = people[0].Id;

        db.Automations.AddRange(
            Automation.Create(hid, ana, "Obavijesti me o računima", "bill.added", "notify", "Dodan je novi račun"),
            Automation.Create(hid, ana, "Prati završene zadatke", "task.completed", "notify", "Zadatak je završen"));

        await db.SaveChangesAsync(cancellationToken);
    }
}

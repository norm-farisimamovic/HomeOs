using HomeOs.Modules.Notes.Domain;
using HomeOs.Modules.Notes.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using HomeOs.Platform.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HomeOs.Modules.Notes.Seeding;

/// <summary>Dev-only: seeds a couple of notes into the demo household.</summary>
public sealed class NotesDemoSeeder(
    IHostEnvironment env, IConfiguration config, IHouseholdLookup households, IMemberDirectory members, NotesDbContext db) : IDataSeeder
{
    public int Order => 150;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!DemoMode.IsEnabled(env, config)) return;
        if (await households.FindHouseholdIdByNameAsync("Demo Home", cancellationToken) is not { } hid) return;
        if (await db.Notes.AnyAsync(n => n.HouseholdId == hid, cancellationToken)) return;

        var people = await members.GetHouseholdMembersAsync(hid, cancellationToken);
        if (people.Count == 0) return;
        var ana = people[0].Id;

        var wifi = Note.Create(hid, ana, "Wi-Fi lozinka", "Mreža: DomWiFi\nLozinka: kucaJeNajbolja123", ["dom"], Visibility.Household);
        wifi.SetPinned(true);
        db.Notes.AddRange(
            wifi,
            Note.Create(hid, ana, "Recept za pitu", "Jufke, sir, jaja, mlijeko. Peći 40 min na 200°C.", ["kuhinja", "recepti"], Visibility.Household),
            Note.Create(hid, ana, "Ideje za vikend", "Izlet na Vrelo Bosne, kino, roštilj.", [], Visibility.Private));

        await db.SaveChangesAsync(cancellationToken);
    }
}

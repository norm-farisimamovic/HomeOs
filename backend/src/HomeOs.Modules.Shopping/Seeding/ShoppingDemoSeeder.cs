using HomeOs.Modules.Shopping.Domain;
using HomeOs.Modules.Shopping.Persistence;
using HomeOs.Platform.Members;
using HomeOs.Platform.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HomeOs.Modules.Shopping.Seeding;

/// <summary>Dev-only: seeds a couple of shared lists into the demo household (idempotent).</summary>
public sealed class ShoppingDemoSeeder(IHostEnvironment env, IHouseholdLookup households, ShoppingDbContext db) : IDataSeeder
{
    public int Order => 160;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!env.IsDevelopment()) return;
        if (await households.FindHouseholdIdByNameAsync("Demo Home", cancellationToken) is not { } hid) return;
        if (await db.Lists.AnyAsync(l => l.HouseholdId == hid, cancellationToken)) return;

        var groceries = ShoppingList.Create(hid, "Kupovina");
        groceries.Items.Add(ShoppingItem.Create(groceries.Id, "Mlijeko"));
        groceries.Items.Add(ShoppingItem.Create(groceries.Id, "Hljeb"));
        groceries.Items.Add(ShoppingItem.Create(groceries.Id, "Kafa"));

        var hardware = ShoppingList.Create(hid, "Željezara");
        hardware.Items.Add(ShoppingItem.Create(hardware.Id, "Sijalice"));
        hardware.Items.Add(ShoppingItem.Create(hardware.Id, "Baterije"));

        db.Lists.AddRange(groceries, hardware);
        await db.SaveChangesAsync(cancellationToken);
    }
}

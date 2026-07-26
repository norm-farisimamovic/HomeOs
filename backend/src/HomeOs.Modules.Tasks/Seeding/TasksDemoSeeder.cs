using HomeOs.Modules.Tasks.Domain;
using HomeOs.Modules.Tasks.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using HomeOs.Platform.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HomeOs.Modules.Tasks.Seeding;

/// <summary>Dev-only: seeds a handful of realistic tasks into the demo household so the app has data to show.</summary>
public sealed class TasksDemoSeeder(
    IHostEnvironment env, IHouseholdLookup households, IMemberDirectory members, TasksDbContext db) : IDataSeeder
{
    /// <inheritdoc />
    public int Order => 110; // after the demo household (20)

    /// <inheritdoc />
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!env.IsDevelopment()) return;

        if (await households.FindHouseholdIdByNameAsync("Demo Home", cancellationToken) is not { } householdId) return;
        if (await db.Tasks.AnyAsync(t => t.HouseholdId == householdId, cancellationToken)) return;

        var people = await members.GetHouseholdMembersAsync(householdId, cancellationToken);
        if (people.Count == 0) return;
        var owner = people[0].Id;
        var other = people.Count > 1 ? people[1].Id : owner;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.Tasks.AddRange(
            TaskItem.Create(householdId, owner, "Pay the internet bill", null, today.AddDays(-1), owner, TaskPriority.High, Visibility.Household, ["bills"]),
            TaskItem.Create(householdId, owner, "Book the boiler service", "Call the Vaillant service line.", today, other, TaskPriority.High, Visibility.Household, ["home"]),
            TaskItem.Create(householdId, owner, "Groceries for the weekend", null, today, other, TaskPriority.Normal, Visibility.Household, ["shopping"]),
            TaskItem.Create(householdId, owner, "Renew car registration", null, today.AddDays(5), other, TaskPriority.Normal, Visibility.Household, []),
            TaskItem.Create(householdId, owner, "Clean out the storage room", null, null, owner, TaskPriority.Low, Visibility.Household, ["home"]));

        var done = TaskItem.Create(householdId, owner, "Take out the recycling", null, today, owner, TaskPriority.Low, Visibility.Household, []);
        done.Complete();
        db.Tasks.Add(done);

        await db.SaveChangesAsync(cancellationToken);
    }
}

using HomeOs.Platform.Access;
using HomeOs.Platform.Members;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HomeOs.Platform.Seeding;

/// <summary>
/// Dev-only: creates a demo household ("Demo Home") with two members so a fresh install has something to
/// explore. Login: <c>demo@homeos.local</c> / <c>Demo1234!</c>. Idempotent; never runs outside Development.
/// </summary>
public sealed class DemoHouseholdSeeder(IHostEnvironment env, UserManager<Member> users, PlatformDbContext db) : IDataSeeder
{
    /// <inheritdoc />
    public int Order => 20;

    /// <inheritdoc />
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!env.IsDevelopment()) return;
        if (await db.Households.AnyAsync(h => h.Name == "Demo Home", cancellationToken)) return;

        var household = new Household("Demo Home");
        db.Households.Add(household);
        await db.SaveChangesAsync(cancellationToken);

        await CreateAsync(household.Id, "demo@imel.ba", "Imel", "Demo", HouseholdRoles.Owner);
        await CreateAsync(household.Id, "faris@homeos.local", "Faris", "Demo", HouseholdRoles.Adult);
    }

    private async Task CreateAsync(Guid householdId, string email, string firstName, string lastName, string role)
    {
        var member = new Member
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = Member.FullName(firstName, lastName),
            HouseholdId = householdId,
        };
        var result = await users.CreateAsync(member, "Demo1234!");
        if (result.Succeeded) await users.AddToRoleAsync(member, role);
    }
}

using HomeOs.Platform.Access;
using HomeOs.Platform.Members;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using HomeOs.Platform.Seeding;

namespace HomeOs.Platform.Seeding;

/// <summary>
/// Creates a demo household ("Demo Home") with two members so a fresh install has something to explore.
/// Login: <c>demo@imel.ba</c> / <c>Demo1234!</c> (override the password with <c>Demo:Password</c>).
/// Idempotent. Runs in Development, or in production when <c>Demo:Enabled=true</c> — see <see cref="DemoMode"/>.
/// </summary>
public sealed class DemoHouseholdSeeder(IHostEnvironment env, IConfiguration config, UserManager<Member> users, PlatformDbContext db) : IDataSeeder
{
    /// <inheritdoc />
    public int Order => 20;

    /// <inheritdoc />
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!DemoMode.IsEnabled(env, config)) return;
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
        var result = await users.CreateAsync(member, config["Demo:Password"] ?? "Demo1234!");
        if (result.Succeeded) await users.AddToRoleAsync(member, role);
    }
}

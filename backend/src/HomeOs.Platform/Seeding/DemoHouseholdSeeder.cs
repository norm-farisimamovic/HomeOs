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

        // Get-or-create so a partially-seeded state (household created but a member missing) self-heals on
        // the next run instead of being locked out by a household-level "already exists" check.
        var household = await db.Households.FirstOrDefaultAsync(h => h.Name == "Demo Home", cancellationToken);
        if (household is null)
        {
            household = new Household("Demo Home");
            db.Households.Add(household);
            await db.SaveChangesAsync(cancellationToken);
        }

        await EnsureMemberAsync(household.Id, "demo@imel.ba", "Imel", "Demo", HouseholdRoles.Owner);
        await EnsureMemberAsync(household.Id, "faris@homeos.local", "Faris", "Demo", HouseholdRoles.Adult);
    }

    private async Task EnsureMemberAsync(Guid householdId, string email, string firstName, string lastName, string role)
    {
        if (await users.FindByEmailAsync(email) is not null) return;

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
        // Empty ("") counts as unset — fall back to the default rather than an invalid empty password.
        var password = config["Demo:Password"];
        if (string.IsNullOrWhiteSpace(password)) password = "Demo1234!";

        var result = await users.CreateAsync(member, password);
        if (result.Succeeded) await users.AddToRoleAsync(member, role);
    }
}

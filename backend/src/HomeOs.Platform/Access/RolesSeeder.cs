using HomeOs.Platform.Seeding;
using Microsoft.AspNetCore.Identity;

namespace HomeOs.Platform.Access;

/// <summary>Seeds the household roles (<see cref="HouseholdRoles"/>) as Identity roles. Idempotent.</summary>
public sealed class RolesSeeder(RoleManager<IdentityRole<Guid>> roleManager) : IDataSeeder
{
    /// <inheritdoc />
    public int Order => 10;

    /// <inheritdoc />
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in HouseholdRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }
}

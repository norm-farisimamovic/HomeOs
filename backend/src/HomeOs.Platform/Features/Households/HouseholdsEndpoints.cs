using HomeOs.Platform.Access;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Features.Households;

/// <summary>
/// Multiple households per person. A person can own several households (e.g. home and work) and switch
/// between them without logging out. Each household still has its own <see cref="Member"/> row; they're
/// tied together by <see cref="Member.PersonId"/>. Only the primary account (the real email) signs in with
/// a password — secondary households are created here and reached via a trusted server-side switch, so
/// email-based login is never ambiguous.
/// </summary>
public static class HouseholdsEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/households").RequireAuthorization().WithTags("Households");

        // Households the current person can switch to (all accounts sharing their PersonId).
        group.MapGet("/switchable", async (ICurrentMember me, PlatformDbContext db, UserManager<Member> users, CancellationToken ct) =>
        {
            var self = await db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id, ct);
            var siblings = await db.Users.AsNoTracking()
                .Where(u => u.PersonId == self.PersonId)
                .Join(db.Households.AsNoTracking(), u => u.HouseholdId, h => h.Id, (u, h) => new { u, h })
                .ToListAsync(ct);

            var result = new List<SwitchableDto>();
            foreach (var s in siblings.OrderBy(s => s.h.Name))
            {
                var roles = await users.GetRolesAsync(s.u);
                result.Add(new SwitchableDto(s.h.Id, s.h.Name, s.u.Id, string.Join(", ", roles), s.u.Id == me.Id));
            }
            return Results.Ok(result);
        }).WithName("SwitchableHouseholds");

        // Create a new household owned by the current person (a linked secondary account).
        group.MapPost("/", async (CreateHouseholdRequest req, ICurrentMember me, PlatformDbContext db,
            UserManager<Member> users, IAppText text, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest();
            var self = await db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id, ct);

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var household = new Household(req.Name.Trim());
            db.Households.Add(household);
            await db.SaveChangesAsync(ct);

            // A linked owner account: no password, a sink email (never mailed / never used to log in),
            // already confirmed. Reached only through the switch endpoint below.
            var linkId = Guid.NewGuid();
            var member = new Member
            {
                UserName = $"hh-{linkId:N}",
                Email = $"{linkId:N}@switch.local",
                EmailConfirmed = true,
                FirstName = self.FirstName,
                LastName = self.LastName,
                DisplayName = self.DisplayName,
                HouseholdId = household.Id,
                PersonId = self.PersonId,
                PreferredCulture = self.PreferredCulture,
                PreferredCurrency = self.PreferredCurrency,
            };
            var created = await users.CreateAsync(member);
            if (!created.Succeeded)
            {
                await tx.RollbackAsync(ct);
                return Results.ValidationProblem(created.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
            }
            await users.AddToRoleAsync(member, HouseholdRoles.Owner);
            await tx.CommitAsync(ct);
            return Results.Ok(new { householdId = household.Id, memberId = member.Id });
        }).WithName("CreateHousehold");

        // Switch the session to another of the person's households.
        group.MapPost("/switch", async (SwitchHouseholdRequest req, ICurrentMember me, PlatformDbContext db,
            SignInManager<Member> signIn, UserManager<Member> users, IAppText text, CancellationToken ct) =>
        {
            var self = await db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id, ct);
            var target = await db.Users.FirstOrDefaultAsync(u => u.HouseholdId == req.HouseholdId && u.PersonId == self.PersonId, ct);
            if (target is null) return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: text["error.auth.forbidden"]);

            // Trusted switch: the target belongs to the same person, so sign in as it (re-issues the cookie
            // with the target's household + roles). No password needed — identity is already established.
            await signIn.SignInAsync(target, isPersistent: true);
            return Results.Ok(new { householdId = target.HouseholdId, memberId = target.Id });
        }).WithName("SwitchHousehold");

        return app;
    }
}

/// <summary>A household the current person can switch into.</summary>
public sealed record SwitchableDto(Guid HouseholdId, string HouseholdName, Guid MemberId, string Roles, bool Current);

/// <summary>Create-household payload.</summary>
public sealed record CreateHouseholdRequest(string Name);

/// <summary>Switch-household payload.</summary>
public sealed record SwitchHouseholdRequest(Guid HouseholdId);

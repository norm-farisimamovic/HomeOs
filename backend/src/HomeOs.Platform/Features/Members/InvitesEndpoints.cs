using System.Globalization;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Platform.Features.Members;

/// <summary>Accept payload.</summary>
public sealed record AcceptInviteRequest(string Password);

/// <summary>Public invite endpoints — an invitee views and accepts without being signed in yet.</summary>
public static class InvitesEndpoints
{
    /// <summary>Maps <c>/api/invites/{token}</c>.</summary>
    public static IEndpointRouteBuilder MapInvitesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invites").WithTags("Invites");

        group.MapGet("/{token}", GetAsync).WithName("GetInvite");
        group.MapPost("/{token}/accept", AcceptAsync).WithName("AcceptInvite");

        return app;
    }

    private static async Task<IResult> GetAsync(string token, PlatformDbContext db, UserManager<Member> userManager, CancellationToken ct)
    {
        var invite = await db.HouseholdInvites.AsNoTracking().FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null || !invite.IsPending) return Results.NotFound();

        var householdName = await db.Households.Where(h => h.Id == invite.HouseholdId).Select(h => h.Name).FirstOrDefaultAsync(ct);
        // If this email already has an account, accepting adds a linked membership (no password needed).
        var accountExists = await userManager.FindByEmailAsync(invite.Email) is not null;
        return Results.Ok(new { householdName, invite.Email, invite.DisplayName, invite.Role, accountExists });
    }

    private static async Task<IResult> AcceptAsync(
        string token, AcceptInviteRequest req, PlatformDbContext db,
        UserManager<Member> userManager, SignInManager<Member> signInManager, IAppText text, CancellationToken ct)
    {
        var invite = await db.HouseholdInvites.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null || !invite.IsPending) return Results.NotFound();

        // Existing account → the same person joins this household as a linked membership (no new email/
        // password). They sign in with their existing account and switch between households.
        var existing = await userManager.FindByEmailAsync(invite.Email);
        if (existing is not null)
        {
            var alreadyHere = await db.Users.FirstOrDefaultAsync(
                u => u.PersonId == existing.PersonId && u.HouseholdId == invite.HouseholdId, ct);
            if (alreadyHere is not null)
            {
                invite.Accept();
                await db.SaveChangesAsync(ct);
                await signInManager.SignInAsync(alreadyHere, isPersistent: true);
                return Results.Ok(new { alreadyHere.Id });
            }

            var linkId = Guid.NewGuid();
            var linked = new Member
            {
                UserName = $"hh-{linkId:N}",
                Email = $"{linkId:N}@switch.local",
                EmailConfirmed = true,
                FirstName = existing.FirstName,
                LastName = existing.LastName,
                DisplayName = existing.DisplayName,
                HouseholdId = invite.HouseholdId,
                PersonId = existing.PersonId,
                PreferredCulture = existing.PreferredCulture,
                PreferredCurrency = existing.PreferredCurrency,
            };
            var link = await userManager.CreateAsync(linked);
            if (!link.Succeeded)
                return Results.ValidationProblem(link.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
            await userManager.AddToRoleAsync(linked, invite.Role);
            invite.Accept();
            await db.SaveChangesAsync(ct);
            await signInManager.SignInAsync(linked, isPersistent: true);
            return Results.Ok(new { linked.Id });
        }

        var member = new Member
        {
            UserName = invite.Email,
            Email = invite.Email,
            EmailConfirmed = true,
            FirstName = invite.FirstName,
            LastName = invite.LastName,
            DisplayName = invite.DisplayName,
            HouseholdId = invite.HouseholdId,
            // Remember the language they're accepting in, so their future emails match.
            PreferredCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
        };
        var created = await userManager.CreateAsync(member, req.Password);
        if (!created.Succeeded)
            return Results.ValidationProblem(created.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

        await userManager.AddToRoleAsync(member, invite.Role);
        invite.Accept();
        await db.SaveChangesAsync(ct);
        await signInManager.SignInAsync(member, isPersistent: true);

        return Results.Ok(new { member.Id });
    }
}

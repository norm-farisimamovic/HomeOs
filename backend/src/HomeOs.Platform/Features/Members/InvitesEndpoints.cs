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

    private static async Task<IResult> GetAsync(string token, PlatformDbContext db, CancellationToken ct)
    {
        var invite = await db.HouseholdInvites.AsNoTracking().FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null || !invite.IsPending) return Results.NotFound();

        var householdName = await db.Households.Where(h => h.Id == invite.HouseholdId).Select(h => h.Name).FirstOrDefaultAsync(ct);
        return Results.Ok(new { householdName, invite.Email, invite.DisplayName, invite.Role });
    }

    private static async Task<IResult> AcceptAsync(
        string token, AcceptInviteRequest req, PlatformDbContext db,
        UserManager<Member> userManager, SignInManager<Member> signInManager, IAppText text, CancellationToken ct)
    {
        var invite = await db.HouseholdInvites.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null || !invite.IsPending) return Results.NotFound();
        if (await userManager.FindByEmailAsync(invite.Email) is not null)
            return Results.Conflict(new { message = text["error.invite.accountExists"] });

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

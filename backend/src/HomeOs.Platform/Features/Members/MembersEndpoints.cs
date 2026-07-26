using System.Globalization;
using System.Net;
using System.Security.Claims;
using HomeOs.Platform.Access;
using HomeOs.Platform.Audit;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HomeOs.Platform.Features.Members;

/// <summary>A household member for the Household page.</summary>
public sealed record MemberListItem(Guid Id, string FirstName, string LastName, string DisplayName, string Email, string Role, bool IsYou);

/// <summary>A pending invitation.</summary>
public sealed record InviteListItem(Guid Id, string Email, string DisplayName, string Role, string CreatedAtUtc);

/// <summary>Invite payload.</summary>
public sealed record InviteRequest(string Email, string FirstName, string LastName, string Role);

/// <summary>Role-change payload.</summary>
public sealed record ChangeRoleRequest(string Role);

/// <summary>Household member management — list, invite, remove, change role. Managing requires Owner/Admin.</summary>
public static class MembersEndpoints
{
    /// <summary>Maps <c>/api/members</c>.</summary>
    public static IEndpointRouteBuilder MapMembersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/members").RequireAuthorization().WithTags("Members");

        group.MapGet("/", ListAsync).WithName("ListMembers");
        group.MapGet("/{id:guid}/avatar", AvatarAsync).WithName("MemberAvatar");
        group.MapGet("/invites", ListInvitesAsync).WithName("ListInvites");
        group.MapPost("/invite", InviteAsync).WithName("InviteMember");
        group.MapDelete("/invites/{id:guid}", CancelInviteAsync).WithName("CancelInvite");
        group.MapPut("/{id:guid}", UpdateMemberAsync).WithName("UpdateMember");
        group.MapPut("/{id:guid}/role", ChangeRoleAsync).WithName("ChangeMemberRole");
        group.MapDelete("/{id:guid}", RemoveAsync).WithName("RemoveMember");
        group.MapPut("/household", RenameHouseholdAsync).WithName("RenameHousehold");

        return app;
    }

    private static async Task<IResult> ListAsync(
        ICurrentMember me, PlatformDbContext db, UserManager<Member> userManager, CancellationToken ct)
    {
        var members = await db.Users.AsNoTracking()
            .Where(u => u.HouseholdId == me.HouseholdId).OrderBy(u => u.DisplayName).ToListAsync(ct);

        var result = new List<MemberListItem>(members.Count);
        foreach (var m in members)
        {
            var roles = await userManager.GetRolesAsync(m);
            result.Add(new MemberListItem(m.Id, m.FirstName, m.LastName, m.DisplayName, m.Email ?? string.Empty, roles.FirstOrDefault() ?? HouseholdRoles.Adult, m.Id == me.Id));
        }
        return Results.Ok(result);
    }

    private static async Task<IResult> AvatarAsync(Guid id, ICurrentMember me, PlatformDbContext db, CancellationToken ct)
    {
        // Only members of the same household can see each other's photos.
        var sameHousehold = await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.HouseholdId == me.HouseholdId, ct);
        if (!sameHousehold) return Results.NotFound();
        var avatar = await db.MemberAvatars.AsNoTracking().FirstOrDefaultAsync(a => a.MemberId == id, ct);
        return avatar is null ? Results.NotFound() : Results.File(avatar.Data, avatar.ContentType);
    }

    private static async Task<IResult> ListInvitesAsync(ClaimsPrincipal user, ICurrentMember me, PlatformDbContext db, CancellationToken ct)
    {
        if (!CanManage(user)) return Results.Forbid();
        var invites = await db.HouseholdInvites.AsNoTracking()
            .Where(i => i.HouseholdId == me.HouseholdId && i.AcceptedAtUtc == null)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new InviteListItem(i.Id, i.Email, i.DisplayName, i.Role, i.CreatedAtUtc.ToString("o")))
            .ToListAsync(ct);
        return Results.Ok(invites);
    }

    private static async Task<IResult> InviteAsync(
        InviteRequest req, ClaimsPrincipal user, ICurrentMember me, PlatformDbContext db,
        UserManager<Member> userManager, IEmailSender email, IConfiguration config, IAppText text, IAuditLog audit, CancellationToken ct)
    {
        if (!CanManage(user)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [text["error.invite.emailRequired"]] });
        if (!HouseholdRoles.All.Contains(req.Role))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = [text["error.invite.unknownRole"]] });

        var normalized = req.Email.Trim().ToLowerInvariant();
        var existing = await userManager.FindByEmailAsync(normalized);
        if (existing is not null && existing.HouseholdId == me.HouseholdId)
            return Results.Conflict(new { message = text["error.invite.exists"] });

        var invite = HouseholdInvite.Create(me.HouseholdId, normalized, req.FirstName, req.LastName, req.Role, me.Id);
        db.HouseholdInvites.Add(invite);
        await db.SaveChangesAsync(ct);

        var householdName = await db.Households.Where(h => h.Id == me.HouseholdId).Select(h => h.Name).FirstOrDefaultAsync(ct) ?? "Home OS";
        var baseUrl = config["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
        var link = $"{baseUrl}/invite/{invite.Token}";

        // The invitee hasn't chosen a language yet, so send in the inviter's current UI language.
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var safeHousehold = WebUtility.HtmlEncode(householdName);
        var html = text.EmailHtml(lang,
            text.T(lang, "email.invite.greeting", WebUtility.HtmlEncode(invite.DisplayName)),
            text.T(lang, "email.invite.line", safeHousehold),
            text.T(lang, "email.invite.cta"), link, showRawLink: true);
        await email.SendAsync(new EmailMessage(normalized, text.T(lang, "email.invite.subject", householdName), html), ct);
        await audit.RecordAsync("member.invited", $"{invite.DisplayName} <{normalized}> ({req.Role})", ct);

        return Results.Ok(new InviteListItem(invite.Id, invite.Email, invite.DisplayName, invite.Role, invite.CreatedAtUtc.ToString("o")));
    }

    private static async Task<IResult> CancelInviteAsync(Guid id, ClaimsPrincipal user, ICurrentMember me, PlatformDbContext db, CancellationToken ct)
    {
        if (!CanManage(user)) return Results.Forbid();
        var invite = await db.HouseholdInvites.FirstOrDefaultAsync(i => i.Id == id && i.HouseholdId == me.HouseholdId, ct);
        if (invite is null) return Results.NotFound();
        db.HouseholdInvites.Remove(invite);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateMemberAsync(
        Guid id, UpdateMemberRequest req, ClaimsPrincipal user, ICurrentMember me, PlatformDbContext db, UserManager<Member> userManager, IAppText text, IAuditLog audit, CancellationToken ct)
    {
        if (!CanManage(user)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["firstName"] = [text["error.profile.nameRequired"]] });

        var member = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.HouseholdId == me.HouseholdId, ct);
        if (member is null) return Results.NotFound();

        member.FirstName = req.FirstName.Trim();
        member.LastName = req.LastName.Trim();
        member.DisplayName = Member.FullName(req.FirstName, req.LastName);

        // Email is the login identity — change it only when a new, non-empty, different value is given.
        var email = req.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(email) && !string.Equals(email, member.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await userManager.FindByEmailAsync(email) is { } clash && clash.Id != member.Id)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = [text["error.invite.exists"]] });
            member.Email = email;
            member.NormalizedEmail = userManager.NormalizeEmail(email);
            member.UserName = email;
            member.NormalizedUserName = userManager.NormalizeName(email);
            member.EmailConfirmed = true; // set by a manager, keep them able to sign in
        }

        var result = await userManager.UpdateAsync(member);
        if (!result.Succeeded)
            return Results.ValidationProblem(result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

        await audit.RecordAsync("member.updated", member.DisplayName, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangeRoleAsync(
        Guid id, ChangeRoleRequest req, ClaimsPrincipal user, ICurrentMember me, PlatformDbContext db, UserManager<Member> userManager, IAppText text, IAuditLog audit, CancellationToken ct)
    {
        if (!CanManage(user)) return Results.Forbid();
        if (!HouseholdRoles.All.Contains(req.Role)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = [text["error.invite.unknownRole"]] });

        var member = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.HouseholdId == me.HouseholdId, ct);
        if (member is null) return Results.NotFound();

        var current = await userManager.GetRolesAsync(member);
        await userManager.RemoveFromRolesAsync(member, current);
        await userManager.AddToRoleAsync(member, req.Role);
        await audit.RecordAsync("member.roleChanged", $"{member.DisplayName} → {req.Role}", ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveAsync(Guid id, ClaimsPrincipal user, ICurrentMember me, PlatformDbContext db, UserManager<Member> userManager, IAppText text, IAuditLog audit, CancellationToken ct)
    {
        if (!CanManage(user)) return Results.Forbid();
        if (id == me.Id) return Results.BadRequest(new { message = text["error.member.removeSelf"] });

        var member = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.HouseholdId == me.HouseholdId, ct);
        if (member is null) return Results.NotFound();

        var roles = await userManager.GetRolesAsync(member);
        if (roles.Contains(HouseholdRoles.Owner)) return Results.BadRequest(new { message = text["error.member.removeOwner"] });

        var name = member.DisplayName;
        await userManager.DeleteAsync(member);
        await audit.RecordAsync("member.removed", name, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RenameHouseholdAsync(
        RenameHouseholdRequest req, ClaimsPrincipal user, ICurrentMember me, PlatformDbContext db, IAuditLog audit, IAppText text, CancellationToken ct)
    {
        if (!CanManage(user)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = [text["error.profile.nameRequired"]] });

        var household = await db.Households.FirstOrDefaultAsync(h => h.Id == me.HouseholdId, ct);
        if (household is null) return Results.NotFound();
        household.Rename(req.Name.Trim());
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync("household.renamed", household.Name, ct);
        return Results.Ok(new { name = household.Name });
    }

    private static bool CanManage(ClaimsPrincipal user) =>
        user.IsInRole(HouseholdRoles.Owner) || user.IsInRole(HouseholdRoles.Admin);
}

/// <summary>Rename-household payload.</summary>
public sealed record RenameHouseholdRequest(string Name);

/// <summary>Edit-member payload (name always; email optional — it's the login identity).</summary>
public sealed record UpdateMemberRequest(string FirstName, string LastName, string? Email);

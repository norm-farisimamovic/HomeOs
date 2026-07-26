using System.Net;
using System.Security.Claims;
using HomeOs.Platform.Access;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Money;
using HomeOs.Platform.Notifications;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HomeOs.Platform.Features.Auth;

/// <summary>Sign-up (creates a household + Owner), sign-in, sign-out, and current-user endpoints.</summary>
public static class AuthEndpoints
{
    /// <summary>Maps the <c>/api/auth</c> endpoints.</summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync).WithName("Register");
        group.MapPost("/confirm-email", ConfirmEmailAsync).WithName("ConfirmEmail");
        group.MapPost("/resend-confirmation", ResendConfirmationAsync).WithName("ResendConfirmation");
        group.MapPost("/forgot-password", ForgotPasswordAsync).WithName("ForgotPassword");
        group.MapPost("/reset-password", ResetPasswordAsync).WithName("ResetPassword");
        group.MapPost("/login", LoginAsync).WithName("Login");
        group.MapPost("/logout", LogoutAsync).RequireAuthorization().WithName("Logout");
        group.MapGet("/me", MeAsync).RequireAuthorization().WithName("Me");
        group.MapPut("/profile", UpdateProfileAsync).RequireAuthorization().WithName("UpdateProfile");
        group.MapPost("/password", ChangePasswordAsync).RequireAuthorization().WithName("ChangePassword");
        group.MapPost("/avatar", UploadAvatarAsync).RequireAuthorization().DisableAntiforgery().WithName("UploadAvatar");
        group.MapDelete("/avatar", DeleteAvatarAsync).RequireAuthorization().WithName("DeleteAvatar");

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<Member> userManager,
        PlatformDbContext db,
        IEmailSender email,
        IConfiguration config,
        IAppText text,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName)
            || string.IsNullOrWhiteSpace(request.HouseholdName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [text["error.register.required"]],
            });
        }

        // First user creates the household and becomes its Owner — atomically.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var household = new Household(request.HouseholdName.Trim());
        db.Households.Add(household);
        await db.SaveChangesAsync(ct);

        var member = new Member
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DisplayName = Member.FullName(request.FirstName, request.LastName),
            HouseholdId = household.Id,
            PreferredCulture = string.IsNullOrWhiteSpace(request.PreferredCulture) ? "bs" : request.PreferredCulture!.Trim(),
        };

        var created = await userManager.CreateAsync(member, request.Password);
        if (!created.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return Results.ValidationProblem(created.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }

        await userManager.AddToRoleAsync(member, HouseholdRoles.Owner);
        await tx.CommitAsync(ct);

        // Strict confirmation: send a verification link; the founder must confirm before signing in.
        await SendConfirmationEmailAsync(member, userManager, email, config, text, ct);
        return Results.Ok(new { requiresConfirmation = true, email = member.Email });
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        UserManager<Member> userManager,
        SignInManager<Member> signInManager,
        PlatformDbContext db,
        IAppText text,
        CancellationToken ct)
    {
        var member = await userManager.FindByIdAsync(request.UserId);
        if (member is null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: text["error.confirm.invalid"]);

        var result = await userManager.ConfirmEmailAsync(member, request.Token);
        if (!result.Succeeded)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: text["error.confirm.expired"]);

        await signInManager.SignInAsync(member, isPersistent: true);
        return Results.Ok(await BuildMeAsync(member, userManager, db, ct));
    }

    private static async Task<IResult> ResendConfirmationAsync(
        ResendConfirmationRequest request,
        UserManager<Member> userManager,
        IEmailSender email,
        IConfiguration config,
        IAppText text,
        CancellationToken ct)
    {
        var member = await userManager.FindByEmailAsync(request.Email.Trim());
        if (member is not null && !await userManager.IsEmailConfirmedAsync(member))
            await SendConfirmationEmailAsync(member, userManager, email, config, text, ct);

        // Always 200 — never reveal whether the email exists.
        return Results.Ok(new { sent = true });
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request, UserManager<Member> userManager, IEmailSender email,
        IConfiguration config, IAppText text, CancellationToken ct)
    {
        var member = await userManager.FindByEmailAsync(request.Email.Trim());
        if (member is not null && await userManager.IsEmailConfirmedAsync(member))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(member);
            var baseUrl = config["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
            var link = $"{baseUrl}/reset-password?userId={member.Id}&token={Uri.EscapeDataString(token)}";
            var lang = member.PreferredCulture;
            var html = text.EmailHtml(lang,
                text.T(lang, "email.reset.greeting", WebUtility.HtmlEncode(member.DisplayName)),
                text.T(lang, "email.reset.line"),
                text.T(lang, "email.reset.cta"), link, showRawLink: true);
            await email.SendAsync(new EmailMessage(member.Email!, text.T(lang, "email.reset.subject"), html), ct);
        }
        // Always 200 — never reveal whether the email exists.
        return Results.Ok(new { sent = true });
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request, UserManager<Member> userManager, IAppText text, CancellationToken ct)
    {
        var member = await userManager.FindByIdAsync(request.UserId);
        if (member is null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: text["error.confirm.invalid"]);

        var result = await userManager.ResetPasswordAsync(member, request.Token, request.NewPassword);
        return result.Succeeded
            ? Results.NoContent()
            : Results.ValidationProblem(result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        SignInManager<Member> signInManager,
        UserManager<Member> userManager,
        PlatformDbContext db,
        IAppText text,
        CancellationToken ct)
    {
        var member = await userManager.FindByEmailAsync(request.Email.Trim());
        if (member is not null)
        {
            var result = await signInManager.PasswordSignInAsync(member, request.Password, request.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
                return Results.Ok(await BuildMeAsync(member, userManager, db, ct));
            if (result.IsNotAllowed)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: text["error.login.unconfirmed"]);
        }

        // Same response whether the email is unknown or the password is wrong (no user enumeration).
        return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: text["error.login.invalid"]);
    }

    // Emails are written in the recipient's own language (their saved PreferredCulture), not the sender's.
    private static async Task SendConfirmationEmailAsync(
        Member member, UserManager<Member> userManager, IEmailSender email, IConfiguration config, IAppText text, CancellationToken ct)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(member);
        var baseUrl = config["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
        var link = $"{baseUrl}/confirm-email?userId={member.Id}&token={Uri.EscapeDataString(token)}";
        var lang = member.PreferredCulture;
        var html = text.EmailHtml(lang,
            text.T(lang, "email.confirm.greeting", WebUtility.HtmlEncode(member.DisplayName)),
            text.T(lang, "email.confirm.line"),
            text.T(lang, "email.confirm.cta"), link, showRawLink: true);
        await email.SendAsync(new EmailMessage(member.Email!, text.T(lang, "email.confirm.subject"), html), ct);
    }

    private static async Task<IResult> LogoutAsync(SignInManager<Member> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(
        ClaimsPrincipal user, UserManager<Member> userManager, PlatformDbContext db, IAppText text, CancellationToken ct)
    {
        var member = await userManager.GetUserAsync(user);
        return member is null
            ? Unauthorized(text)
            : Results.Ok(await BuildMeAsync(member, userManager, db, ct));
    }

    /// <summary>A localized 401 (so the SPA shows a friendly message, never the raw "Unauthorized").</summary>
    private static IResult Unauthorized(IAppText text) =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: text["error.auth.unauthorized"]);

    private static async Task<IResult> UpdateProfileAsync(
        ProfileRequest req, ClaimsPrincipal user, UserManager<Member> userManager, PlatformDbContext db, IAppText text, CancellationToken ct)
    {
        var member = await userManager.GetUserAsync(user);
        if (member is null) return Unauthorized(text);
        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["firstName"] = [text["error.profile.nameRequired"]] });

        member.FirstName = req.FirstName.Trim();
        member.LastName = req.LastName.Trim();
        member.DisplayName = Member.FullName(req.FirstName, req.LastName);
        if (!string.IsNullOrWhiteSpace(req.PreferredCulture)) member.PreferredCulture = req.PreferredCulture.Trim();
        if (!string.IsNullOrWhiteSpace(req.PreferredCurrency)) member.PreferredCurrency = Currencies.Normalize(req.PreferredCurrency);
        if (Enum.TryParse<DigestFrequency>(req.DigestFrequency, ignoreCase: true, out var digest)) member.DigestFrequency = digest;
        await userManager.UpdateAsync(member);

        return Results.Ok(await BuildMeAsync(member, userManager, db, ct));
    }

    private static async Task<IResult> UploadAvatarAsync(
        IFormFile? file, ClaimsPrincipal user, ICurrentMember me, PlatformDbContext db, IAppText text, CancellationToken ct)
    {
        if (!me.IsAuthenticated) return Unauthorized(text);
        if (file is null || file.Length == 0) return Results.BadRequest();
        if (file.Length > 2 * 1024 * 1024) return Results.Problem(statusCode: 400, title: text["error.avatar.tooLarge"]);
        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return Results.Problem(statusCode: 400, title: text["error.avatar.notImage"]);

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var avatar = await db.MemberAvatars.FirstOrDefaultAsync(a => a.MemberId == me.Id, ct);
        if (avatar is null)
        {
            db.MemberAvatars.Add(new MemberAvatar { MemberId = me.Id, Data = ms.ToArray(), ContentType = file.ContentType, UpdatedAtUtc = DateTimeOffset.UtcNow });
        }
        else
        {
            avatar.Data = ms.ToArray();
            avatar.ContentType = file.ContentType;
            avatar.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { updatedAt = DateTimeOffset.UtcNow });
    }

    private static async Task<IResult> DeleteAvatarAsync(ICurrentMember me, PlatformDbContext db, IAppText text, CancellationToken ct)
    {
        if (!me.IsAuthenticated) return Unauthorized(text);
        var avatar = await db.MemberAvatars.FirstOrDefaultAsync(a => a.MemberId == me.Id, ct);
        if (avatar is not null) { db.MemberAvatars.Remove(avatar); await db.SaveChangesAsync(ct); }
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest req, ClaimsPrincipal user, UserManager<Member> userManager, IAppText text)
    {
        var member = await userManager.GetUserAsync(user);
        if (member is null) return Unauthorized(text);

        var result = await userManager.ChangePasswordAsync(member, req.CurrentPassword, req.NewPassword);
        return result.Succeeded
            ? Results.NoContent()
            : Results.ValidationProblem(result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
    }

    private static async Task<MeResponse> BuildMeAsync(
        Member member, UserManager<Member> userManager, PlatformDbContext db, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(member);
        var household = await db.Households.AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == member.HouseholdId, ct);
        var hasAvatar = await db.MemberAvatars.AsNoTracking().AnyAsync(a => a.MemberId == member.Id, ct);

        return new MeResponse(
            member.Id,
            member.Email ?? string.Empty,
            member.FirstName,
            member.LastName,
            member.DisplayName,
            member.HouseholdId,
            household?.Name ?? string.Empty,
            roles.ToList(),
            member.PreferredCulture,
            Currencies.Normalize(member.PreferredCurrency),
            member.DigestFrequency.ToString(),
            hasAvatar);
    }
}

/// <summary>Registration payload: creates a household and its first (Owner) member.</summary>
public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName, string HouseholdName, string? PreferredCulture);

/// <summary>Login payload.</summary>
public sealed record LoginRequest(string Email, string Password, bool RememberMe);

/// <summary>The current authenticated member and household.</summary>
public sealed record MeResponse(
    Guid Id, string Email, string FirstName, string LastName, string DisplayName, Guid HouseholdId, string HouseholdName,
    IReadOnlyList<string> Roles, string PreferredCulture, string PreferredCurrency, string DigestFrequency, bool HasAvatar);

/// <summary>Profile edit payload.</summary>
public sealed record ProfileRequest(string FirstName, string LastName, string? PreferredCulture, string? PreferredCurrency, string? DigestFrequency);

/// <summary>Change-password payload.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Email-confirmation payload (from the emailed link).</summary>
public sealed record ConfirmEmailRequest(string UserId, string Token);

/// <summary>Resend-confirmation payload.</summary>
public sealed record ResendConfirmationRequest(string Email);

/// <summary>Forgot-password payload (emails a reset link).</summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>Reset-password payload (from the emailed link).</summary>
public sealed record ResetPasswordRequest(string UserId, string Token, string NewPassword);

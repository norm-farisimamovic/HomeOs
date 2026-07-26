using System.Text.Json;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Platform.Access;

/// <summary>Registers ASP.NET Core Identity with cookie authentication for the SPA/PWA.</summary>
public static class IdentitySetup
{
    /// <summary>
    /// Adds Identity (members + roles) backed by <see cref="PlatformDbContext"/> and a hardened
    /// application cookie. The cookie is httpOnly, and API requests get 401/403 status codes rather
    /// than HTML login redirects.
    /// </summary>
    public static IServiceCollection AddHomeOsIdentity(this IServiceCollection services)
    {
        services.AddIdentity<Member, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.SignIn.RequireConfirmedAccount = true; // founders must confirm their email before signing in
            })
            .AddEntityFrameworkStores<PlatformDbContext>()
            .AddErrorDescriber<LocalizedIdentityErrorDescriber>() // password/email errors in the request's language
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "HomeOs.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Always in production (HTTPS)
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            // API clients get a localized ProblemDetails body (not an HTML login redirect, and not a
            // bare status code — otherwise the SPA falls back to the raw English "Unauthorized" text).
            options.Events.OnRedirectToLogin = ctx =>
                WriteProblemAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized, "error.auth.unauthorized");
            options.Events.OnRedirectToAccessDenied = ctx =>
                WriteProblemAsync(ctx.HttpContext, StatusCodes.Status403Forbidden, "error.auth.forbidden");
        });

        services.AddHttpContextAccessor();
        services.AddAuthorization();
        return services;
    }

    /// <summary>Writes a localized RFC-9457 ProblemDetails body for an auth failure (401/403).</summary>
    private static async Task WriteProblemAsync(HttpContext http, int status, string key)
    {
        if (http.Response.HasStarted) return;
        // Request-localization middleware runs before auth, so CurrentUICulture is already the request's.
        var text = http.RequestServices.GetRequiredService<IAppText>();
        http.Response.StatusCode = status;
        http.Response.ContentType = "application/problem+json";
        var json = JsonSerializer.Serialize(new { type = "about:blank", title = text[key], status });
        await http.Response.WriteAsync(json);
    }
}

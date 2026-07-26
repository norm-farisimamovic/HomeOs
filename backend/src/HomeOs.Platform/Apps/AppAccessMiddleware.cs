using System.Globalization;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Platform.Apps;

/// <summary>
/// The single place that enforces the household's app choices. For any request to an app's API it checks the
/// app is enabled and that the household granted the capability the verb needs (read for safe methods, write
/// otherwise). Core surfaces and non-app paths pass straight through. This lives in the kernel so disabling or
/// restricting an app needs no change in the app itself — extensibility never becomes a way around access.
/// </summary>
public sealed class AppAccessMiddleware(RequestDelegate next, IAppRegistry registry)
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };

    /// <summary>Runs the check, then the rest of the pipeline.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var manifest = registry.ForApiPath(context.Request.Path);

        // Not an app-owned API path, a core surface, or an unauthenticated request → nothing to enforce here.
        if (manifest is null || manifest.IsCore || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var me = context.RequestServices.GetRequiredService<ICurrentMember>();
        if (!me.IsAuthenticated)
        {
            await next(context);
            return;
        }

        var access = context.RequestServices.GetRequiredService<IAppAccess>();

        if (!await access.IsEnabledAsync(me.HouseholdId, manifest.Id, context.RequestAborted))
        {
            await Deny(context, "error.app.disabled");
            return;
        }

        var needed = SafeMethods.Contains(context.Request.Method) ? manifest.ReadCapability : manifest.WriteCapability;
        if (!await access.HasCapabilityAsync(me.HouseholdId, manifest.Id, needed, context.RequestAborted))
        {
            await Deny(context, "error.app.capabilityDenied");
            return;
        }

        await next(context);
    }

    private static async Task Deny(HttpContext context, string messageKey)
    {
        var text = context.RequestServices.GetRequiredService<IAppText>();
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            title = text.T(culture, messageKey),
            status = StatusCodes.Status403Forbidden,
        }, context.RequestAborted);
    }
}

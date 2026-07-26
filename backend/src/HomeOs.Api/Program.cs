using System.Threading.RateLimiting;
using HomeOs.Platform;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Features.Apps;
using HomeOs.Platform.Features.Attachments;
using HomeOs.Platform.Features.Audit;
using HomeOs.Platform.Features.Auth;
using HomeOs.Platform.Features.Assistant;
using HomeOs.Platform.Features.Digest;
using HomeOs.Platform.Features.Households;
using HomeOs.Platform.Features.Links;
using HomeOs.Platform.Features.Members;
using HomeOs.Platform.Features.Money;
using HomeOs.Platform.Features.Notifications;
using HomeOs.Platform.Features.Scoreboard;
using HomeOs.Platform.Features.Search;
using HomeOs.Platform.Features.Weather;
using HomeOs.Platform.Notifications;
using HomeOs.Platform.Persistence;
using HomeOs.Platform.Startup;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Load local user-secrets for EVERY environment, not just Development. Reason: launching without the
// `http` launch profile (IDE Run/Debug, `dotnet run --no-launch-profile`) leaves the environment as
// Production, where the framework skips user-secrets — and our DB connection string + SMTP creds live
// there, so startup would crash with "Missing connection string 'HomeOsDb'". Harmless in real
// production: no secrets store is present, so this is a no-op and env vars/appsettings win.
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);

// Default to :5080 so a bare/IDE launch never falls back to Kestrel's :5000. Guarded so it only applies
// when NOTHING else set a URL — ASPNETCORE_URLS (dev tests, the `http` launch profile) and the deploy
// host's bind address always win, so this can't hijack the port in production.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
    && string.IsNullOrEmpty(builder.Configuration["Urls"]))
{
    builder.WebHost.UseUrls("http://localhost:5080");
}

// Structured logging from the first line (see .claude/skills/dotnet-backend → logging).
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Household languages — each member picks their own; default Bosnian (see i18n conventions).
string[] supportedCultures = ["bs", "en"];

builder.Services.AddHomeOsPlatform(builder.Configuration);
// Every HomeOs.Modules.* app self-registers here — no per-module lines to edit when one is added.
builder.Services.AddHomeOsModules(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rate limiting: a generous global cap per IP, and a much tighter one on the auth endpoints
// (login/register/reset) to blunt brute-force + credential-stuffing. Rejections return 429.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isAuth = context.Request.Path.StartsWithSegments("/api/auth");
        return RateLimitPartition.GetFixedWindowLimiter((isAuth ? "auth:" : "gen:") + ip, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = isAuth ? 10 : 120,
                Window = TimeSpan.FromSeconds(isAuth ? 30 : 10),
                QueueLimit = 0,
            });
    });
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

builder.Services.AddCors(options => options.AddPolicy("web", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                 ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddRequestLocalization(options => options
    .SetDefaultCulture("bs")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

var app = builder.Build();

// Create the database if missing, apply migrations, then run data seeders (config-gated).
await app.InitializeHomeOsDatabaseAsync();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();   // unhandled errors → RFC-9457 ProblemDetails
app.UseStatusCodePages();

// Baseline security headers on every response. HSTS is production-only (dev runs on plain HTTP).
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    var path = context.Request.Path;
    if (path.StartsWithSegments("/swagger")) { /* Swagger (dev) needs inline scripts/styles — no CSP */ }
    else if (path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs"))
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
    else // the SPA (served same-origin in production) needs its own bundle + inline styles
        headers["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; " +
            "font-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve the built SPA (wwwroot) same-origin in production; API routes are matched first, everything else
// falls back to index.html so client-side routing works. In dev the SPA runs separately on Vite.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRequestLocalization();
app.UseCors("web");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Enforce the household's app choices (disabled app / revoked capability → 403) before any app endpoint runs.
app.UseMiddleware<AppAccessMiddleware>();

app.MapAuthEndpoints();
app.MapAppsEndpoints();
app.MapDigestEndpoints();
app.MapLinksEndpoints();
app.MapAssistantEndpoints();
app.MapMembersEndpoints();
app.MapInvitesEndpoints();
app.MapNotificationsEndpoints();
app.MapSearchEndpoints();
app.MapAuditEndpoints();
app.MapCurrenciesEndpoints();
app.MapWeatherEndpoints();
app.MapAttachmentEndpoints();
app.MapScoreboardEndpoints();
app.MapHouseholdsEndpoints();
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapHomeOsModules();

// Liveness: the process is up (no dependencies). Readiness: dependencies (DB) are reachable.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapGet("/api/ping", () => Results.Ok(new
{
    message = "Home OS API is running.",
    utc = DateTimeOffset.UtcNow
}))
.WithTags("Diagnostics")
.WithName("Ping");

// SPA client-side routing: any non-API, non-file request returns index.html (no-op in dev — wwwroot is empty).
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Exposed so integration tests can reference the API host via <c>WebApplicationFactory</c>.</summary>
public partial class Program;

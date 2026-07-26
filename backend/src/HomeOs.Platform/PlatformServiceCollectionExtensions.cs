using HomeOs.Platform.Access;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Assistant;
using HomeOs.Platform.Audit;
using HomeOs.Platform.Digest;
using HomeOs.Platform.Events;
using HomeOs.Platform.Links;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using HomeOs.Platform.Notifications;
using HomeOs.Platform.Persistence;
using HomeOs.Platform.Seeding;
using HomeOs.Platform.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Platform;

/// <summary>
/// Composition root for the Home OS platform kernel. The API host calls
/// <see cref="AddHomeOsPlatform"/> once; app modules register themselves separately (from M1).
/// </summary>
public static class PlatformServiceCollectionExtensions
{
    /// <summary>
    /// Registers the platform kernel. In M0 this is persistence only; M1 adds members, access
    /// (authN → roles → capabilities → visibility), the event bus, app registry, notifications,
    /// localization and real-time.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configuration">App configuration (needs connection string <c>HomeOsDb</c>).</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the connection string is missing.</exception>
    public static IServiceCollection AddHomeOsPlatform(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeOsDb")
            ?? throw new InvalidOperationException(
                "Missing connection string 'HomeOsDb'. Set it via user-secrets or configuration.");

        // Explicit server version (no startup AutoDetect round-trip → app boots even if MySQL is down).
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 42));

        services.AddDbContextPool<PlatformDbContext>(options =>
            options.UseMySql(connectionString, serverVersion));

        // Localized server text (API error titles + emails), used by Identity and the endpoints below.
        services.AddSingleton<IAppText, AppText>();

        // Identity + cookie auth, and seed the household roles on startup.
        services.AddHomeOsIdentity();
        services.AddDataSeeder<RolesSeeder>();

        // Kernel services: current member, member directory, household lookup, in-process event bus + handlers.
        services.AddScoped<ICurrentMember, CurrentMember>();
        services.AddScoped<IMemberDirectory, MemberDirectory>();
        services.AddScoped<IHouseholdLookup, HouseholdLookup>();
        services.AddScoped<IEventBus, InProcessEventBus>();
        services.AddEventHandlers(typeof(PlatformServiceCollectionExtensions).Assembly);
        services.AddHomeOsEmail(configuration);

        // Notifications: in-app feed + email + real-time push (SignalR).
        services.AddSignalR();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IShareNotifier, ShareNotifier>();

        // Audit log (owner/admin). The AppActivity → audit handler is picked up by AddEventHandlers above.
        services.AddScoped<IAuditLog, AuditLog>();
        // Records every create/update/delete on module data. Modules add it to their DbContext (see AddAuditing).
        services.AddSingleton<AuditInterceptor>();

        // App registry + per-household access control (enable/disable + capability grants).
        services.AddSingleton<IAppRegistry, AppRegistry>();
        services.AddScoped<IAppAccess, AppAccess>();

        // Cross-app object links (the "connected web").
        services.AddScoped<IEntityLinks, EntityLinks>();

        // Gamification scoreboard (points for completed chores, etc.).
        services.AddScoped<Scoreboard.IScoreboard, Scoreboard.Scoreboard>();

        // AI assistant (LLM tool-use over kernel contracts; no-op until Assistant:ApiKey is set).
        // Works with any OpenAI-compatible provider (Groq/Gemini/OpenRouter/Ollama — free tiers) or Anthropic.
        services.AddHttpClient("assistant");
        services.AddScoped<IAssistant, AssistantService>();

        // Weather proxy for the dashboard widget (Open-Meteo, keyless).
        services.AddHttpClient("weather");

        // "What's coming up" digest: builder + the scheduled sender (opt-in daily/weekly).
        services.AddScoped<IDigestService, DigestService>();
        services.AddHostedService<DigestDispatcher>();

        // Migrate the platform context on startup; seed a demo household in Development.
        services.AddSingleton(new MigratableContext(typeof(PlatformDbContext)));
        services.AddDataSeeder<DemoHouseholdSeeder>();

        return services;
    }

    /// <summary>
    /// Registers a data seeder that runs once on startup (after migrations). Seeders must be
    /// idempotent. Call from a module's registration. See <c>docs/SEEDING.md</c>.
    /// </summary>
    public static IServiceCollection AddDataSeeder<TSeeder>(this IServiceCollection services)
        where TSeeder : class, IDataSeeder
    {
        services.AddScoped<IDataSeeder, TSeeder>();
        return services;
    }
}

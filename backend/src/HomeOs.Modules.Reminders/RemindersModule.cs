using HomeOs.Modules.Reminders.Calendar;
using HomeOs.Modules.Reminders.Digest;
using HomeOs.Modules.Reminders.Dispatch;
using HomeOs.Modules.Reminders.Features;
using HomeOs.Modules.Reminders.Persistence;
using HomeOs.Modules.Reminders.Search;
using HomeOs.Modules.Reminders.Seeding;
using HomeOs.Platform;
using HomeOs.Platform.Audit;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Calendar;
using HomeOs.Platform.Digest;
using HomeOs.Platform.Events;
using HomeOs.Platform.Reminders;
using HomeOs.Platform.Search;
using HomeOs.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Modules.Reminders;

/// <summary>Composition for the Reminders app — registered by the host the same way any app is.</summary>
public static class RemindersModule
{
    public static IServiceCollection AddRemindersModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeOsDb")
            ?? throw new InvalidOperationException("Missing connection string 'HomeOsDb'.");

        services.AddDbContextPool<RemindersDbContext>((sp, options) =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42))).AddAuditing(sp));

        services.AddSingleton(new MigratableContext(typeof(RemindersDbContext)));
        services.AddEventHandlers(typeof(RemindersModule).Assembly);
        services.AddDataSeeder<RemindersDemoSeeder>();

        // Contribute reminders to the shared calendar (see ICalendarSource).
        services.AddScoped<ICalendarSource, RemindersCalendarSource>();

        // Expose the kernel reminder capability so any app can schedule reminders (see IReminderService).
        services.AddScoped<IReminderService, ReminderService>();

        // Background job that fires due reminders (in-app + email) once each.
        services.AddHostedService<ReminderDispatcher>();
        // Contribute reminders to global search.
        services.AddScoped<ISearchProvider, RemindersSearchProvider>();
        // Contribute upcoming reminders to the digest.
        services.AddScoped<IUpcomingProvider, RemindersUpcomingProvider>();

        // Announce this app to the platform.
        services.AddSingleton<IAppModule, RemindersAppModule>();

        return services;
    }

    public static IEndpointRouteBuilder MapRemindersModule(this IEndpointRouteBuilder app) => app.MapRemindersEndpoints();
}

/// <summary>Reminders app manifest.</summary>
public sealed class RemindersAppModule : IAppModule
{
    /// <inheritdoc />
    public AppManifest Manifest { get; } = new(
        "reminders", "nav.reminders", "apps.desc.reminders", "Bell", "var(--m-reminders)",
        "/reminders", "/api/reminders", ["read:reminders", "write:reminders"]);
}

/// <summary>Auto-discovered host wiring for the Reminders app.</summary>
public sealed class RemindersHostModule : HomeOs.Platform.Startup.IHostModule
{
    /// <inheritdoc />
    public void Add(IServiceCollection services, IConfiguration configuration) => services.AddRemindersModule(configuration);
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapRemindersModule();
}

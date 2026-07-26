using HomeOs.Modules.Calendar.Features;
using HomeOs.Modules.Calendar.Persistence;
using HomeOs.Modules.Calendar.Search;
using HomeOs.Modules.Calendar.Seeding;
using HomeOs.Platform;
using HomeOs.Platform.Audit;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Events;
using HomeOs.Platform.Search;
using HomeOs.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Modules.Calendar;

/// <summary>Composition for the Calendar app — registered by the host the same way any app is.</summary>
public static class CalendarModule
{
    public static IServiceCollection AddCalendarModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeOsDb")
            ?? throw new InvalidOperationException("Missing connection string 'HomeOsDb'.");

        services.AddDbContextPool<CalendarDbContext>((sp, options) =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42))).AddAuditing(sp));

        services.AddSingleton(new MigratableContext(typeof(CalendarDbContext)));
        services.AddEventHandlers(typeof(CalendarModule).Assembly);
        services.AddDataSeeder<CalendarDemoSeeder>();
        services.AddScoped<ISearchProvider, CalendarSearchProvider>();

        // Announce this app to the platform.
        services.AddSingleton<IAppModule, CalendarAppModule>();

        return services;
    }

    public static IEndpointRouteBuilder MapCalendarModule(this IEndpointRouteBuilder app) => app.MapCalendarEndpoints();
}

/// <summary>Calendar app manifest.</summary>
public sealed class CalendarAppModule : IAppModule
{
    /// <inheritdoc />
    public AppManifest Manifest { get; } = new(
        "calendar", "nav.calendar", "apps.desc.calendar", "Calendar", "var(--m-calendar)",
        "/calendar", "/api/calendar", ["read:calendar", "write:calendar"]);
}

/// <summary>Auto-discovered host wiring for the Calendar app.</summary>
public sealed class CalendarHostModule : HomeOs.Platform.Startup.IHostModule
{
    /// <inheritdoc />
    public void Add(IServiceCollection services, IConfiguration configuration) => services.AddCalendarModule(configuration);
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapCalendarModule();
}

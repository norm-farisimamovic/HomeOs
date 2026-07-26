using HomeOs.Modules.LifeAdmin.Calendar;
using HomeOs.Modules.LifeAdmin.Features;
using HomeOs.Modules.LifeAdmin.Persistence;
using HomeOs.Modules.LifeAdmin.Search;
using HomeOs.Modules.LifeAdmin.Seeding;
using HomeOs.Platform;
using HomeOs.Platform.Audit;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Calendar;
using HomeOs.Platform.Events;
using HomeOs.Platform.Search;
using HomeOs.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Modules.LifeAdmin;

/// <summary>Composition for the Life-admin app — registered by the host the same way any app is.</summary>
public static class LifeAdminModule
{
    public static IServiceCollection AddLifeAdminModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeOsDb")
            ?? throw new InvalidOperationException("Missing connection string 'HomeOsDb'.");

        services.AddDbContextPool<LifeAdminDbContext>((sp, options) =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42))).AddAuditing(sp));

        services.AddSingleton(new MigratableContext(typeof(LifeAdminDbContext)));
        services.AddEventHandlers(typeof(LifeAdminModule).Assembly);
        services.AddDataSeeder<LifeAdminDemoSeeder>();

        // Expiry/renewal dates show on the shared calendar (see ICalendarSource).
        services.AddScoped<ICalendarSource, LifeCalendarSource>();
        // Contribute records to global search.
        services.AddScoped<ISearchProvider, LifeSearchProvider>();

        // Announce this app to the platform.
        services.AddSingleton<IAppModule, LifeAdminAppModule>();

        return services;
    }

    public static IEndpointRouteBuilder MapLifeAdminModule(this IEndpointRouteBuilder app) => app.MapLifeAdminEndpoints();
}

/// <summary>Life-admin app manifest.</summary>
public sealed class LifeAdminAppModule : IAppModule
{
    /// <inheritdoc />
    public AppManifest Manifest { get; } = new(
        "life", "nav.life", "apps.desc.life", "Archive", "var(--m-life)",
        "/life", "/api/life", ["read:life", "write:life"]);
}

/// <summary>Auto-discovered host wiring for the Life-admin app.</summary>
public sealed class LifeAdminHostModule : HomeOs.Platform.Startup.IHostModule
{
    /// <inheritdoc />
    public void Add(IServiceCollection services, IConfiguration configuration) => services.AddLifeAdminModule(configuration);
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapLifeAdminModule();
}

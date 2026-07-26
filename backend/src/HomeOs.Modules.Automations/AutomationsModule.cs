using HomeOs.Modules.Automations.Features;
using HomeOs.Modules.Automations.Persistence;
using HomeOs.Modules.Automations.Seeding;
using HomeOs.Platform;
using HomeOs.Platform.Audit;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Events;
using HomeOs.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Modules.Automations;

/// <summary>Composition for the Automations app — registered by the host the same way any app is.</summary>
public static class AutomationsModule
{
    public static IServiceCollection AddAutomationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeOsDb")
            ?? throw new InvalidOperationException("Missing connection string 'HomeOsDb'.");

        services.AddDbContextPool<AutomationsDbContext>((sp, options) =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42))).AddAuditing(sp));

        services.AddSingleton(new MigratableContext(typeof(AutomationsDbContext)));
        services.AddEventHandlers(typeof(AutomationsModule).Assembly); // registers AutomationRunner (IEventHandler<AppActivity>)
        services.AddDataSeeder<AutomationsDemoSeeder>();

        // Announce this app to the platform.
        services.AddSingleton<IAppModule, AutomationsAppModule>();

        return services;
    }

    public static IEndpointRouteBuilder MapAutomationsModule(this IEndpointRouteBuilder app) => app.MapAutomationsEndpoints();
}

/// <summary>Automations app manifest.</summary>
public sealed class AutomationsAppModule : IAppModule
{
    /// <inheritdoc />
    public AppManifest Manifest { get; } = new(
        "automations", "nav.automations", "apps.desc.automations", "Zap", "var(--text-3)",
        "/automations", "/api/automations", ["read:automations", "write:automations"]);
}

/// <summary>Auto-discovered host wiring for the Automations app.</summary>
public sealed class AutomationsHostModule : HomeOs.Platform.Startup.IHostModule
{
    /// <inheritdoc />
    public void Add(IServiceCollection services, IConfiguration configuration) => services.AddAutomationsModule(configuration);
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapAutomationsModule();
}

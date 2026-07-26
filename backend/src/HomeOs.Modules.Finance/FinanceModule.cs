using HomeOs.Modules.Finance.Calendar;
using HomeOs.Modules.Finance.Digest;
using HomeOs.Modules.Finance.Dispatch;
using HomeOs.Modules.Finance.Features;
using HomeOs.Modules.Finance.Persistence;
using HomeOs.Modules.Finance.Search;
using HomeOs.Modules.Finance.Seeding;
using HomeOs.Platform;
using HomeOs.Platform.Audit;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Calendar;
using HomeOs.Platform.Digest;
using HomeOs.Platform.Events;
using HomeOs.Platform.Search;
using HomeOs.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Modules.Finance;

/// <summary>Composition for the Finance app — registered by the host the same way any app is.</summary>
public static class FinanceModule
{
    public static IServiceCollection AddFinanceModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeOsDb")
            ?? throw new InvalidOperationException("Missing connection string 'HomeOsDb'.");

        services.AddDbContextPool<FinanceDbContext>((sp, options) =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42))).AddAuditing(sp));

        services.AddSingleton(new MigratableContext(typeof(FinanceDbContext)));
        services.AddEventHandlers(typeof(FinanceModule).Assembly);
        services.AddDataSeeder<FinanceDemoSeeder>();

        // Contribute bills' due dates to the shared calendar (see ICalendarSource).
        services.AddScoped<ICalendarSource, BillsCalendarSource>();
        // Contribute bills to global search.
        services.AddScoped<ISearchProvider, FinanceSearchProvider>();
        // Alert the household before a bill is due (in-app + email).
        services.AddHostedService<BillDispatcher>();
        // Contribute upcoming bills to the digest.
        services.AddScoped<IUpcomingProvider, FinanceUpcomingProvider>();

        // Announce this app to the platform.
        services.AddSingleton<IAppModule, FinanceAppModule>();

        return services;
    }

    public static IEndpointRouteBuilder MapFinanceModule(this IEndpointRouteBuilder app) => app.MapFinanceEndpoints();
}

/// <summary>Finance app manifest.</summary>
public sealed class FinanceAppModule : IAppModule
{
    /// <inheritdoc />
    public AppManifest Manifest { get; } = new(
        "finance", "nav.finance", "apps.desc.finance", "Wallet", "var(--m-finance)",
        "/finance", "/api/finance", ["read:finance", "write:finance"]);
}

/// <summary>Auto-discovered host wiring for the Finance app.</summary>
public sealed class FinanceHostModule : HomeOs.Platform.Startup.IHostModule
{
    /// <inheritdoc />
    public void Add(IServiceCollection services, IConfiguration configuration) => services.AddFinanceModule(configuration);
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapFinanceModule();
}

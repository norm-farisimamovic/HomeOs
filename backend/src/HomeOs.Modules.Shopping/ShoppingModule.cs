using HomeOs.Modules.Shopping.Features;
using HomeOs.Modules.Shopping.Persistence;
using HomeOs.Modules.Shopping.Search;
using HomeOs.Modules.Shopping.Seeding;
using HomeOs.Platform;
using HomeOs.Platform.Audit;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Search;
using HomeOs.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Modules.Shopping;

/// <summary>
/// Composition for the Shopping-lists app. This module was added <em>after</em> the platform existed and needs
/// no change to the host or any other module — the reference implementation of "new apps are first-class citizens".
/// </summary>
public static class ShoppingModule
{
    public static IServiceCollection AddShoppingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeOsDb")
            ?? throw new InvalidOperationException("Missing connection string 'HomeOsDb'.");

        services.AddDbContextPool<ShoppingDbContext>((sp, options) =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42))).AddAuditing(sp));

        services.AddSingleton(new MigratableContext(typeof(ShoppingDbContext)));
        services.AddDataSeeder<ShoppingDemoSeeder>();
        // Contribute lists to global search (the app self-surfaces the moment it registers this).
        services.AddScoped<ISearchProvider, ShoppingSearchProvider>();
        // Announce this app to the platform.
        services.AddSingleton<IAppModule, ShoppingAppModule>();

        return services;
    }

    public static IEndpointRouteBuilder MapShoppingModule(this IEndpointRouteBuilder app) => app.MapShoppingEndpoints();
}

/// <summary>Shopping app manifest.</summary>
public sealed class ShoppingAppModule : IAppModule
{
    /// <inheritdoc />
    public AppManifest Manifest { get; } = new(
        "shopping", "nav.shopping", "apps.desc.shopping", "ShoppingCart", "var(--m-life)",
        "/shopping", "/api/shopping", ["read:shopping", "write:shopping"]);
}

/// <summary>Auto-discovered host wiring for the Shopping app.</summary>
public sealed class ShoppingHostModule : IHostModule
{
    /// <inheritdoc />
    public void Add(IServiceCollection services, IConfiguration configuration) => services.AddShoppingModule(configuration);
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapShoppingModule();
}

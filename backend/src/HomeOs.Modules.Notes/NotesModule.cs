using HomeOs.Modules.Notes.Features;
using HomeOs.Modules.Notes.Persistence;
using HomeOs.Modules.Notes.Search;
using HomeOs.Modules.Notes.Seeding;
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

namespace HomeOs.Modules.Notes;

/// <summary>Composition for the Notes app — registered by the host the same way any app is.</summary>
public static class NotesModule
{
    public static IServiceCollection AddNotesModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeOsDb")
            ?? throw new InvalidOperationException("Missing connection string 'HomeOsDb'.");

        services.AddDbContextPool<NotesDbContext>((sp, options) =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42))).AddAuditing(sp));

        services.AddSingleton(new MigratableContext(typeof(NotesDbContext)));
        services.AddEventHandlers(typeof(NotesModule).Assembly);
        services.AddDataSeeder<NotesDemoSeeder>();
        services.AddScoped<ISearchProvider, NotesSearchProvider>();

        // Announce this app to the platform.
        services.AddSingleton<IAppModule, NotesAppModule>();

        return services;
    }

    public static IEndpointRouteBuilder MapNotesModule(this IEndpointRouteBuilder app) => app.MapNotesEndpoints();
}

/// <summary>Notes app manifest.</summary>
public sealed class NotesAppModule : IAppModule
{
    /// <inheritdoc />
    public AppManifest Manifest { get; } = new(
        "notes", "nav.notes", "apps.desc.notes", "StickyNote", "var(--m-notes)",
        "/notes", "/api/notes", ["read:notes", "write:notes"]);
}

/// <summary>Auto-discovered host wiring for the Notes app.</summary>
public sealed class NotesHostModule : HomeOs.Platform.Startup.IHostModule
{
    /// <inheritdoc />
    public void Add(IServiceCollection services, IConfiguration configuration) => services.AddNotesModule(configuration);
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapNotesModule();
}

using HomeOs.Modules.Tasks.Calendar;
using HomeOs.Modules.Tasks.Digest;
using HomeOs.Modules.Tasks.Features;
using HomeOs.Modules.Tasks.Persistence;
using HomeOs.Modules.Tasks.Search;
using HomeOs.Modules.Tasks.Seeding;
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

namespace HomeOs.Modules.Tasks;

/// <summary>Composition for the Tasks app — registered by the host the same way any app would be.</summary>
public static class TasksModule
{
    /// <summary>Registers the Tasks DbContext, event handlers, migration target and demo seeder.</summary>
    public static IServiceCollection AddTasksModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeOsDb")
            ?? throw new InvalidOperationException("Missing connection string 'HomeOsDb'.");

        services.AddDbContextPool<TasksDbContext>((sp, options) =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42))).AddAuditing(sp));

        services.AddSingleton(new MigratableContext(typeof(TasksDbContext)));
        services.AddEventHandlers(typeof(TasksModule).Assembly);
        services.AddDataSeeder<TasksDemoSeeder>();

        // Contribute due-dated tasks to the shared calendar (see ICalendarSource).
        services.AddScoped<ICalendarSource, TasksCalendarSource>();
        // Contribute tasks to global search.
        services.AddScoped<ISearchProvider, TasksSearchProvider>();
        // Contribute upcoming tasks to the digest.
        services.AddScoped<IUpcomingProvider, TasksUpcomingProvider>();
        // Let the assistant create tasks ("napravi zadatak…").
        services.AddScoped<HomeOs.Platform.Assistant.IAssistantTool, Assistant.AddTaskTool>();

        // Announce this app (and the Kanban board view, which is a second surface over the same task data).
        services.AddSingleton<IAppModule, TasksAppModule>();
        services.AddSingleton<IAppModule, KanbanAppModule>();

        return services;
    }

    /// <summary>Maps the Tasks endpoints.</summary>
    public static IEndpointRouteBuilder MapTasksModule(this IEndpointRouteBuilder app) => app.MapTasksEndpoints();
}

/// <summary>Tasks app manifest.</summary>
public sealed class TasksAppModule : IAppModule
{
    /// <inheritdoc />
    public AppManifest Manifest { get; } = new(
        "tasks", "nav.tasks", "apps.desc.tasks", "CheckSquare", "var(--m-tasks)",
        "/tasks", "/api/tasks", ["read:tasks", "write:tasks"]);
}

/// <summary>Kanban board manifest — a frontend-only view over the Tasks data (no API of its own).</summary>
public sealed class KanbanAppModule : IAppModule
{
    /// <inheritdoc />
    public AppManifest Manifest { get; } = new(
        "kanban", "nav.boards", "apps.desc.kanban", "Kanban", "var(--m-boards)",
        "/boards", null, []);
}

/// <summary>Auto-discovered host wiring for the Tasks app.</summary>
public sealed class TasksHostModule : HomeOs.Platform.Startup.IHostModule
{
    /// <inheritdoc />
    public void Add(IServiceCollection services, IConfiguration configuration) => services.AddTasksModule(configuration);
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapTasksModule();
}

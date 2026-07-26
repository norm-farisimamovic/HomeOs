using HomeOs.Modules.Chat.Features;
using HomeOs.Modules.Chat.Persistence;
using HomeOs.Platform;
using HomeOs.Platform.Apps;
using HomeOs.Platform.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeOs.Modules.Chat;

/// <summary>Composition for the household Chat app — another plug-in module, no host/other-module changes.</summary>
public static class ChatModule
{
    public static IServiceCollection AddChatModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HomeOsDb")
            ?? throw new InvalidOperationException("Missing connection string 'HomeOsDb'.");

        // No auditing interceptor here — chat is high-volume and shouldn't flood the audit log.
        services.AddDbContextPool<ChatDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 42))));

        services.AddSingleton(new MigratableContext(typeof(ChatDbContext)));
        services.AddSingleton<IAppModule, ChatAppModule>();

        return services;
    }

    public static IEndpointRouteBuilder MapChatModule(this IEndpointRouteBuilder app) => app.MapChatEndpoints();
}

/// <summary>Chat app manifest.</summary>
public sealed class ChatAppModule : IAppModule
{
    /// <inheritdoc />
    public AppManifest Manifest { get; } = new(
        "chat", "nav.chat", "apps.desc.chat", "MessageCircle", "var(--m-boards)",
        "/chat", "/api/chat", ["read:chat", "write:chat"]);
}

/// <summary>Auto-discovered host wiring for the Chat app.</summary>
public sealed class ChatHostModule : IHostModule
{
    /// <inheritdoc />
    public void Add(IServiceCollection services, IConfiguration configuration) => services.AddChatModule(configuration);
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints.MapChatModule();
}

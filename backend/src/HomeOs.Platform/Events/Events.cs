using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HomeOs.Platform.Events;

/// <summary>Marker for a domain event. Events are public contracts other apps may depend on.</summary>
public interface IDomainEvent;

/// <summary>In-process publisher. Publishers never know who handles their events.</summary>
public interface IEventBus
{
    /// <summary>Dispatches <paramref name="domainEvent"/> to every registered handler (isolated, best-effort).</summary>
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>Handles a domain event of type <typeparamref name="TEvent"/>. Must be idempotent + isolated.</summary>
public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
    /// <summary>Reacts to the event.</summary>
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves and invokes all <see cref="IEventHandler{TEvent}"/> for a published event. A failing handler is
/// logged and swallowed so it never rolls back the publisher — dispatch events *after* <c>SaveChanges</c>.
/// </summary>
public sealed class InProcessEventBus(IServiceProvider services, ILogger<InProcessEventBus> logger) : IEventBus
{
    /// <inheritdoc />
    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handleMethod = handlerType.GetMethod(nameof(IEventHandler<IDomainEvent>.Handle))!;

        foreach (var handler in services.GetServices(handlerType))
        {
            if (handler is null) continue;
            try
            {
                await (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event handler {Handler} failed for {Event}",
                    handler.GetType().Name, domainEvent.GetType().Name);
            }
        }
    }
}

/// <summary>DI helpers for the event bus.</summary>
public static class EventBusRegistration
{
    /// <summary>Registers every <see cref="IEventHandler{TEvent}"/> implementation in the given assembly.</summary>
    public static IServiceCollection AddEventHandlers(this IServiceCollection services, System.Reflection.Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var iface in type.GetInterfaces()
                         .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>)))
            {
                services.AddScoped(iface, type);
            }
        }
        return services;
    }
}

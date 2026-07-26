namespace HomeOs.Platform.Events;

/// <summary>
/// A generic "something happened in the home" signal that any app publishes alongside its own typed
/// events. Cross-cutting consumers (the Automations engine, an activity feed) subscribe to just this one
/// kernel event and match on <see cref="Kind"/> — so they never reference individual app modules.
/// </summary>
/// <param name="HouseholdId">Where it happened (tenancy scope for matching rules).</param>
/// <param name="ActorMemberId">Who did it, if known.</param>
/// <param name="Kind">Dotted trigger key, e.g. <c>task.completed</c>, <c>bill.added</c>, <c>event.scheduled</c>.</param>
/// <param name="Title">Human label of the thing (task title, bill name…).</param>
/// <param name="Link">In-app route to the thing, e.g. <c>/tasks</c>.</param>
public sealed record AppActivity(Guid HouseholdId, Guid? ActorMemberId, string Kind, string Title, string? Link)
    : IDomainEvent;

using HomeOs.Platform.Events;

namespace HomeOs.Modules.Finance.Contracts;

// Public contracts other apps may subscribe to (e.g. a future automation: bill due → create a task).

/// <summary>Raised after a transaction is recorded.</summary>
public sealed record TransactionAdded(Guid TransactionId, Guid HouseholdId, decimal Amount, string Category) : IDomainEvent;

/// <summary>Raised after a bill/subscription is added.</summary>
public sealed record BillAdded(Guid BillId, Guid HouseholdId, string Name, DateOnly NextDue) : IDomainEvent;

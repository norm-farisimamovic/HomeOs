using HomeOs.Modules.Tasks.Contracts;
using HomeOs.Platform.Events;
using HomeOs.Platform.Scoreboard;

namespace HomeOs.Modules.Tasks.Scoring;

/// <summary>
/// Awards scoreboard points when a task is completed (and revokes them if it's re-opened). Points go to the
/// task's assignee — or whoever ticked it off when it's unassigned. A flat 10 points per task keeps the
/// household leaderboard simple and fair. Demonstrates the platform pattern: the Tasks app reacts to its own
/// domain event and uses the kernel <see cref="IScoreboard"/> — the kernel never learns what a "task" is.
/// </summary>
public sealed class TaskCompletedPointsHandler(IScoreboard scoreboard) : IEventHandler<TaskCompleted>
{
    private const int PointsPerTask = 10;

    /// <inheritdoc />
    public Task Handle(TaskCompleted domainEvent, CancellationToken cancellationToken)
    {
        var earner = domainEvent.AssigneeId ?? domainEvent.CompletedById;
        return scoreboard.AwardAsync(domainEvent.HouseholdId, earner, "task", domainEvent.TaskId, PointsPerTask, cancellationToken);
    }
}

/// <summary>Revokes a task's points when it's un-completed, so the leaderboard stays honest.</summary>
public sealed class TaskReopenedPointsHandler(IScoreboard scoreboard) : IEventHandler<TaskReopened>
{
    /// <inheritdoc />
    public Task Handle(TaskReopened domainEvent, CancellationToken cancellationToken) =>
        scoreboard.RevokeAsync(domainEvent.HouseholdId, "task", domainEvent.TaskId, cancellationToken);
}

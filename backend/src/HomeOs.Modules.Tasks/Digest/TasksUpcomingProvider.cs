using HomeOs.Modules.Tasks.Domain;
using HomeOs.Modules.Tasks.Persistence;
using HomeOs.Platform.Digest;
using HomeOs.Platform.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Tasks.Digest;

/// <summary>Contributes a member's upcoming, not-yet-done tasks to their digest.</summary>
public sealed class TasksUpcomingProvider(TasksDbContext db) : IUpcomingProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<UpcomingItem>> GetUpcomingAsync(
        Guid householdId, Guid memberId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tasks = await db.Tasks.AsNoTracking()
            .Where(t => t.HouseholdId == householdId && t.Status != TaskItemStatus.Done
                && t.DueDate != null && t.DueDate >= from && t.DueDate <= to
                && (t.OwnerId == memberId || t.AssigneeId == memberId || t.Visibility == Visibility.Household))
            .ToListAsync(ct);

        return tasks.Select(t => new UpcomingItem("tasks", t.Title, t.DueDate!.Value, "task")).ToList();
    }
}

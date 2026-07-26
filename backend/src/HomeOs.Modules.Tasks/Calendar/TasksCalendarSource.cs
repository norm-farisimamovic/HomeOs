using HomeOs.Modules.Tasks.Persistence;
using HomeOs.Platform.Calendar;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Members;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Tasks.Calendar;

/// <summary>Contributes tasks with a due date to the shared calendar (respecting the member's visibility).</summary>
public sealed class TasksCalendarSource(ICurrentMember me, TasksDbContext db) : ICalendarSource
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarItem>> GetItemsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var q = db.Tasks.AsNoTracking()
            .Where(t => t.HouseholdId == me.HouseholdId && t.DueDate != null && t.DueDate >= from && t.DueDate <= to);

        q = me.IsManager
            ? q.Where(t => t.Visibility != Visibility.Private || t.OwnerId == me.Id || t.AssigneeId == me.Id)
            : q.Where(t => t.OwnerId == me.Id || t.AssigneeId == me.Id || t.Visibility == Visibility.Household);

        var tasks = await q.ToListAsync(ct);
        return tasks
            .Select(t => new CalendarItem("tasks", t.Id, t.Title, t.DueDate!.Value, "task", null, t.IsDone))
            .ToList();
    }
}

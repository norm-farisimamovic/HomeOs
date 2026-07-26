using HomeOs.Platform.Entities;

namespace HomeOs.Modules.Tasks.Domain;

/// <summary>Task importance.</summary>
public enum TaskPriority { Low = 0, Normal = 1, High = 2 }

/// <summary>How often a task repeats. <see cref="None"/> is a one-off.</summary>
public enum TaskRecurrence { None = 0, Daily = 1, Weekly = 2, Monthly = 3, Yearly = 4 }

/// <summary>Where a task sits in its lifecycle (also drives the Kanban view later).</summary>
public enum TaskItemStatus { Todo = 0, Doing = 1, Done = 2 }

/// <summary>A household to-do. A first-class <see cref="IHomeObject"/> so other apps can link to it.</summary>
public sealed class TaskItem : IHomeObject
{
    private readonly List<string> _tags = [];

    private TaskItem() { } // EF

    /// <summary>Creates a task owned by <paramref name="ownerId"/> in <paramref name="householdId"/>.</summary>
    public static TaskItem Create(
        Guid householdId, Guid ownerId, string title, string? description, DateOnly? dueDate,
        Guid? assigneeId, TaskPriority priority, Visibility visibility, IEnumerable<string>? tags,
        TaskRecurrence recurrence = TaskRecurrence.None, Guid? parentId = null, Guid? boardId = null)
    {
        var task = new TaskItem
        {
            HouseholdId = householdId,
            OwnerId = ownerId,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DueDate = dueDate,
            AssigneeId = assigneeId,
            Priority = priority,
            Visibility = visibility,
            Recurrence = recurrence,
            ParentId = parentId,
            BoardId = boardId,
        };
        if (tags is not null) task._tags.AddRange(tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct());
        return task;
    }

    /// <inheritdoc />
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <inheritdoc />
    public string ObjectType => "task";

    /// <inheritdoc />
    public Guid HouseholdId { get; private set; }

    /// <summary>Member who created the task.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Member responsible for the task (optional).</summary>
    public Guid? AssigneeId { get; private set; }

    /// <summary>Short title.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Optional details.</summary>
    public string? Description { get; private set; }

    /// <summary>Optional due date (surfaced on the calendar).</summary>
    public DateOnly? DueDate { get; private set; }

    /// <summary>Importance.</summary>
    public TaskPriority Priority { get; private set; } = TaskPriority.Normal;

    /// <summary>Lifecycle status.</summary>
    public TaskItemStatus Status { get; private set; } = TaskItemStatus.Todo;

    /// <summary>How often the task repeats (<see cref="TaskRecurrence.None"/> = one-off).</summary>
    public TaskRecurrence Recurrence { get; private set; } = TaskRecurrence.None;

    /// <summary>Parent task id when this is a sub-task; null for a top-level task.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>The Kanban board this task belongs to; null = the default "General" board.</summary>
    public Guid? BoardId { get; private set; }

    /// <summary>Moves the task to a board (or off boards when null).</summary>
    public void SetBoard(Guid? boardId) => BoardId = boardId;

    /// <summary>Who can see it.</summary>
    public Visibility Visibility { get; private set; } = Visibility.Household;

    /// <summary>Free-form tags.</summary>
    public IReadOnlyList<string> Tags => _tags;

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>When it was completed (UTC), if done.</summary>
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    /// <summary>True once done.</summary>
    public bool IsDone => Status == TaskItemStatus.Done;

    /// <summary>Overdue = has a past due date and isn't done.</summary>
    public bool IsOverdue(DateOnly today) => DueDate is { } d && d < today && !IsDone;

    /// <summary>Applies an edit.</summary>
    public void Update(string title, string? description, DateOnly? dueDate, Guid? assigneeId,
        TaskPriority priority, Visibility visibility, IEnumerable<string> tags,
        TaskRecurrence recurrence = TaskRecurrence.None)
    {
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DueDate = dueDate;
        AssigneeId = assigneeId;
        Priority = priority;
        Visibility = visibility;
        Recurrence = recurrence;
        _tags.Clear();
        _tags.AddRange(tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct());
    }

    /// <summary>
    /// Marks complete. A recurring task with a due date instead rolls its due date to the next occurrence
    /// after <paramref name="today"/> and stays open, so it reappears for the next cycle.
    /// </summary>
    public void Complete(DateOnly? today = null)
    {
        if (Recurrence != TaskRecurrence.None && DueDate is { } due)
        {
            var floor = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var next = NextOccurrence(due, Recurrence);
            for (var guard = 0; next <= floor && guard < 600; guard++)
                next = NextOccurrence(next, Recurrence);
            DueDate = next;
            Status = TaskItemStatus.Todo;
            CompletedAtUtc = null;
            return;
        }
        Status = TaskItemStatus.Done;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    private static DateOnly NextOccurrence(DateOnly date, TaskRecurrence recurrence) => recurrence switch
    {
        TaskRecurrence.Daily => date.AddDays(1),
        TaskRecurrence.Weekly => date.AddDays(7),
        TaskRecurrence.Monthly => date.AddMonths(1),
        TaskRecurrence.Yearly => date.AddYears(1),
        _ => date,
    };

    /// <summary>Reopens a completed task.</summary>
    public void Reopen() { Status = TaskItemStatus.Todo; CompletedAtUtc = null; }

    /// <summary>Moves the task to a lifecycle column (drives the Kanban board).</summary>
    public void MoveTo(TaskItemStatus status)
    {
        Status = status;
        CompletedAtUtc = status == TaskItemStatus.Done ? DateTimeOffset.UtcNow : null;
    }
}

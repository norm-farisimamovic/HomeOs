namespace HomeOs.Modules.Tasks.Features;

/// <summary>A task as returned to clients (dates as ISO strings; assignee enriched with a name).</summary>
/// <remarks><c>CanEdit</c>/<c>CanDelete</c> are computed for the requesting member so the UI only shows
/// actions they're allowed to take (delete = author or a manager; the server enforces it regardless).</remarks>
public sealed record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    string? DueDate,
    Guid? AssigneeId,
    string? AssigneeName,
    string Priority,
    string Status,
    bool IsDone,
    bool IsOverdue,
    IReadOnlyList<string> Tags,
    string Visibility,
    string Recurrence,
    Guid? ParentId,
    int SubtaskDone,
    int SubtaskTotal,
    Guid? BoardId,
    Guid OwnerId,
    bool CanEdit,
    bool CanDelete);

/// <summary>A Kanban board.</summary>
public sealed record BoardDto(Guid Id, string Name, string Color);

/// <summary>Create/update payload for a board.</summary>
public sealed record SaveBoardRequest(string Name, string? Color);

/// <summary>Dashboard counts for the current household + member.</summary>
public sealed record TasksSummaryDto(int DueToday, int Overdue, int OpenTotal, int DoneTotal);

/// <summary>Create payload. <c>ParentId</c> makes it a sub-task; <c>BoardId</c> puts it on a board.</summary>
public sealed record CreateTaskRequest(
    string Title, string? Description, string? DueDate, Guid? AssigneeId,
    string? Priority, IReadOnlyList<string>? Tags, string? Visibility, string? Recurrence, Guid? ParentId, Guid? BoardId);

/// <summary>Update payload.</summary>
public sealed record UpdateTaskRequest(
    string Title, string? Description, string? DueDate, Guid? AssigneeId,
    string? Priority, IReadOnlyList<string>? Tags, string? Visibility, string? Recurrence, Guid? BoardId);

/// <summary>Move-to-column payload (Kanban): <c>Todo</c> / <c>Doing</c> / <c>Done</c>.</summary>
public sealed record SetStatusRequest(string Status);

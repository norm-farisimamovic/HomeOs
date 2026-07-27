using HomeOs.Modules.Tasks.Contracts;
using HomeOs.Modules.Tasks.Domain;
using HomeOs.Modules.Tasks.Persistence;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Events;
using HomeOs.Platform.Localization;
using HomeOs.Platform.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Tasks.Features;

/// <summary>Tasks CRUD — household-scoped, member-visible, event-publishing.</summary>
public static class TasksEndpoints
{
    /// <summary>Maps <c>/api/tasks</c>.</summary>
    public static IEndpointRouteBuilder MapTasksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks").RequireAuthorization().WithTags("Tasks");

        group.MapGet("/", ListAsync).WithName("ListTasks");
        group.MapGet("/summary", SummaryAsync).WithName("TasksSummary");
        group.MapPost("/", CreateAsync).WithName("CreateTask");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateTask");
        group.MapPost("/{id:guid}/toggle", ToggleAsync).WithName("ToggleTask");
        group.MapPost("/{id:guid}/status", SetStatusAsync).WithName("SetTaskStatus");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteTask");

        group.MapGet("/boards", ListBoardsAsync).WithName("ListBoards");
        group.MapPost("/boards", CreateBoardAsync).WithName("CreateBoard");
        group.MapPut("/boards/{id:guid}", UpdateBoardAsync).WithName("UpdateBoard");
        group.MapDelete("/boards/{id:guid}", DeleteBoardAsync).WithName("DeleteBoard");

        return app;
    }

    private static async Task<IResult> ListBoardsAsync(ICurrentMember me, TasksDbContext db, CancellationToken ct)
    {
        var boards = await db.Boards.AsNoTracking().Where(b => b.HouseholdId == me.HouseholdId)
            .OrderBy(b => b.CreatedAtUtc).ToListAsync(ct);
        return Results.Ok(boards.Select(b => new BoardDto(b.Id, b.Name, b.Color)).ToList());
    }

    private static async Task<IResult> CreateBoardAsync(SaveBoardRequest req, ICurrentMember me, TasksDbContext db, IAppText text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = [text["error.tasks.titleRequired"]] });
        var board = Board.Create(me.HouseholdId, req.Name, req.Color ?? "var(--m-boards)");
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/tasks/boards/{board.Id}", new BoardDto(board.Id, board.Name, board.Color));
    }

    private static async Task<IResult> UpdateBoardAsync(Guid id, SaveBoardRequest req, ICurrentMember me, TasksDbContext db, IAppText text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = [text["error.tasks.titleRequired"]] });
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == id && b.HouseholdId == me.HouseholdId, ct);
        if (board is null) return Results.NotFound();
        board.Update(req.Name, req.Color ?? board.Color);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new BoardDto(board.Id, board.Name, board.Color));
    }

    private static async Task<IResult> DeleteBoardAsync(Guid id, ICurrentMember me, TasksDbContext db, CancellationToken ct)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == id && b.HouseholdId == me.HouseholdId, ct);
        if (board is null) return Results.NotFound();
        // Tasks on the board fall back to the default "General" board rather than being deleted.
        var orphans = await db.Tasks.Where(t => t.BoardId == id).ToListAsync(ct);
        foreach (var task in orphans) task.SetBoard(null);
        db.Boards.Remove(board);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListAsync(ICurrentMember me, TasksDbContext db, IMemberDirectory directory, CancellationToken ct)
    {
        var today = Today();
        var tasks = await VisibleTo(db, me).ToListAsync(ct);
        var names = await directory.GetNamesAsync(me.HouseholdId, ct);
        // Sub-task progress per parent (done / total), computed once for the whole list.
        var childStats = tasks.Where(t => t.ParentId is { })
            .GroupBy(t => t.ParentId!.Value)
            .ToDictionary(g => g.Key, g => (Done: g.Count(t => t.IsDone), Total: g.Count()));
        var ordered = tasks
            .OrderBy(t => t.DueDate is null)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.Priority)
            .ThenByDescending(t => t.CreatedAtUtc)
            .Select(t =>
            {
                var stats = childStats.GetValueOrDefault(t.Id);
                return t.ToDto(today, names, me, stats.Done, stats.Total);
            })
            .ToList();
        return Results.Ok(ordered);
    }

    private static async Task<IResult> SummaryAsync(ICurrentMember me, TasksDbContext db, CancellationToken ct)
    {
        var today = Today();
        var mine = await VisibleTo(db, me).ToListAsync(ct);
        var open = mine.Where(t => !t.IsDone).ToList();
        return Results.Ok(new TasksSummaryDto(
            DueToday: open.Count(t => t.DueDate == today),
            Overdue: open.Count(t => t.DueDate is { } d && d < today),
            OpenTotal: open.Count,
            DoneTotal: mine.Count(t => t.IsDone)));
    }

    private static async Task<IResult> CreateAsync(
        CreateTaskRequest req, ICurrentMember me, TasksDbContext db, IMemberDirectory directory, IEventBus bus, IAppText text, CancellationToken ct)
    {
        if (Validate(req.Title, req.Description, text) is { } problem) return problem;

        var task = TaskItem.Create(me.HouseholdId, me.Id, req.Title, req.Description, ParseDate(req.DueDate),
            req.AssigneeId, ParsePriority(req.Priority), ParseVisibility(req.Visibility), req.Tags, ParseRecurrence(req.Recurrence), req.ParentId, req.BoardId);

        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new TaskCreated(task.Id, task.HouseholdId, task.OwnerId, task.AssigneeId, task.DueDate, task.Title), ct);
        if (task.AssigneeId is { } newAssignee)
            await bus.PublishAsync(new TaskAssigned(task.Id, task.HouseholdId, newAssignee, me.Id, task.Title, task.DueDate), ct);

        var names = await directory.GetNamesAsync(me.HouseholdId, ct);
        return Results.Created($"/api/tasks/{task.Id}", task.ToDto(Today(), names, me));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateTaskRequest req, ICurrentMember me, TasksDbContext db, IMemberDirectory directory, IEventBus bus, IAppText text, CancellationToken ct)
    {
        if (Validate(req.Title, req.Description, text) is { } problem) return problem;

        var task = await Editable(db, me, id, ct);
        if (task is null) return Results.NotFound();

        var previousAssignee = task.AssigneeId;
        task.Update(req.Title, req.Description, ParseDate(req.DueDate), req.AssigneeId,
            ParsePriority(req.Priority), ParseVisibility(req.Visibility), req.Tags ?? [], ParseRecurrence(req.Recurrence));
        task.SetBoard(req.BoardId);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new TaskUpdated(task.Id, task.HouseholdId), ct);
        // Newly assigned to someone (not just re-saved) → notify that person.
        if (task.AssigneeId is { } assignee && assignee != previousAssignee)
            await bus.PublishAsync(new TaskAssigned(task.Id, task.HouseholdId, assignee, me.Id, task.Title, task.DueDate), ct);

        var names = await directory.GetNamesAsync(me.HouseholdId, ct);
        return Results.Ok(task.ToDto(Today(), names, me));
    }

    private static async Task<IResult> ToggleAsync(
        Guid id, ICurrentMember me, TasksDbContext db, IMemberDirectory directory, IEventBus bus, CancellationToken ct)
    {
        var task = await Editable(db, me, id, ct);
        if (task is null) return Results.NotFound();

        // Ticking off = completing this occurrence; a recurring task then rolls forward instead of going Done.
        var wasDone = task.IsDone;
        if (wasDone) task.Reopen(); else task.Complete(Today());
        await db.SaveChangesAsync(ct);
        if (!wasDone)
        {
            await bus.PublishAsync(new TaskCompleted(task.Id, task.HouseholdId, task.AssigneeId, task.Title, me.Id), ct);
            await bus.PublishAsync(new AppActivity(task.HouseholdId, me.Id, "task.completed", task.Title, "/tasks"), ct);
        }
        else
        {
            await bus.PublishAsync(new TaskReopened(task.Id, task.HouseholdId), ct);
        }

        var names = await directory.GetNamesAsync(me.HouseholdId, ct);
        return Results.Ok(task.ToDto(Today(), names, me));
    }

    private static async Task<IResult> SetStatusAsync(
        Guid id, SetStatusRequest req, ICurrentMember me, TasksDbContext db, IMemberDirectory directory, IEventBus bus, CancellationToken ct)
    {
        var task = await Editable(db, me, id, ct);
        if (task is null) return Results.NotFound();

        var status = Enum.TryParse<TaskItemStatus>(req.Status, ignoreCase: true, out var s) ? s : TaskItemStatus.Todo;
        var wasDone = task.IsDone;
        task.MoveTo(status);
        await db.SaveChangesAsync(ct);

        if (task.IsDone && !wasDone)
        {
            await bus.PublishAsync(new TaskCompleted(task.Id, task.HouseholdId, task.AssigneeId, task.Title, me.Id), ct);
            await bus.PublishAsync(new AppActivity(task.HouseholdId, me.Id, "task.completed", task.Title, "/tasks"), ct);
        }
        else if (wasDone && !task.IsDone)
        {
            await bus.PublishAsync(new TaskReopened(task.Id, task.HouseholdId), ct);
        }
        else
        {
            await bus.PublishAsync(new TaskUpdated(task.Id, task.HouseholdId), ct);
        }

        var names = await directory.GetNamesAsync(me.HouseholdId, ct);
        return Results.Ok(task.ToDto(Today(), names, me));
    }

    private static async Task<IResult> DeleteAsync(Guid id, ICurrentMember me, TasksDbContext db, IEventBus bus, CancellationToken ct)
    {
        var task = await Deletable(db, me, id, ct);
        if (task is null) return Results.NotFound();

        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new TaskDeleted(task.Id, task.HouseholdId), ct);
        return Results.NoContent();
    }

    // ---- helpers ----

    // Role-based visibility (on top of the hard household boundary):
    //   Owner/Admin  → everything in the household EXCEPT another member's Private items.
    //   everyone else → their own items (owner/assignee) + anything shared "Whole home".
    private static IQueryable<TaskItem> VisibleTo(TasksDbContext db, ICurrentMember me)
    {
        var scoped = db.Tasks.AsNoTracking().Where(t => t.HouseholdId == me.HouseholdId);
        return me.IsManager
            ? scoped.Where(t => t.Visibility != Visibility.Private || t.OwnerId == me.Id || t.AssigneeId == me.Id)
            : scoped.Where(t => t.OwnerId == me.Id || t.AssigneeId == me.Id || t.Visibility == Visibility.Household);
    }

    // Editable = in the household AND (a manager on a non-private item, or the owner/assignee).
    private static async Task<TaskItem?> Editable(TasksDbContext db, ICurrentMember me, Guid id, CancellationToken ct)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.HouseholdId == me.HouseholdId, ct);
        if (task is null) return null;
        if (me.IsManager && task.Visibility != Visibility.Private) return task;
        return task.OwnerId == me.Id || task.AssigneeId == me.Id ? task : null;
    }

    // Deletable = the author (owner) or a manager (Owner/Admin) on a non-private item — NOT the assignee.
    private static async Task<TaskItem?> Deletable(TasksDbContext db, ICurrentMember me, Guid id, CancellationToken ct)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.HouseholdId == me.HouseholdId, ct);
        if (task is null) return null;
        if (me.IsManager && task.Visibility != Visibility.Private) return task;
        return task.OwnerId == me.Id ? task : null;
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static IResult? Validate(string? title, string? description, IAppText text)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(title)) errors["title"] = [text["error.tasks.titleRequired"]];
        else if (title.Trim().Length > 200) errors["title"] = [text["error.tasks.titleTooLong"]];
        if (description is { Length: > 2000 }) errors["description"] = [text["error.tasks.detailsTooLong"]];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, out var d) ? d : null;

    private static TaskPriority ParsePriority(string? value) =>
        Enum.TryParse<TaskPriority>(value, ignoreCase: true, out var p) ? p : TaskPriority.Normal;

    private static Visibility ParseVisibility(string? value) =>
        Enum.TryParse<Visibility>(value, ignoreCase: true, out var v) ? v : Visibility.Household;

    private static TaskRecurrence ParseRecurrence(string? value) =>
        Enum.TryParse<TaskRecurrence>(value, ignoreCase: true, out var r) ? r : TaskRecurrence.None;

    private static TaskDto ToDto(this TaskItem t, DateOnly today, IReadOnlyDictionary<Guid, string> names, ICurrentMember me,
        int subtaskDone = 0, int subtaskTotal = 0)
    {
        var managerHere = me.IsManager && t.Visibility != Visibility.Private;
        var canDelete = managerHere || t.OwnerId == me.Id;                       // author or a manager
        var canEdit = canDelete || t.AssigneeId == me.Id;                        // …plus the assignee
        return new(
            t.Id, t.Title, t.Description, t.DueDate?.ToString("yyyy-MM-dd"),
            t.AssigneeId,
            t.AssigneeId is { } a && names.TryGetValue(a, out var name) ? name : null,
            t.Priority.ToString(), t.Status.ToString(), t.IsDone, t.IsOverdue(today), t.Tags, t.Visibility.ToString(),
            t.Recurrence.ToString(), t.ParentId, subtaskDone, subtaskTotal, t.BoardId, t.OwnerId, canEdit, canDelete);
    }
}

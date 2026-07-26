using System.Text.Json.Nodes;
using HomeOs.Modules.Tasks.Contracts;
using HomeOs.Modules.Tasks.Domain;
using HomeOs.Modules.Tasks.Persistence;
using HomeOs.Platform.Assistant;
using HomeOs.Platform.Entities;
using HomeOs.Platform.Events;
using HomeOs.Platform.Members;

namespace HomeOs.Modules.Tasks.Assistant;

/// <summary>
/// Assistant tool: create a to-do task. Registering this makes the Tasks app <em>actionable</em> by the
/// assistant ("napravi zadatak za sutra") with no change to the kernel — it runs for the current member and
/// publishes <see cref="TaskCreated"/> just like the normal create endpoint, so the task shows up everywhere.
/// </summary>
public sealed class AddTaskTool(ICurrentMember me, TasksDbContext db, IEventBus bus) : IAssistantTool
{
    public string Name => "add_task";

    public string Description => "Create a to-do task for the household (with an optional due date and priority). " +
        "Use this when the user wants a task/to-do, not a reminder.";

    public JsonObject Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["title"] = new JsonObject { ["type"] = "string", ["description"] = "The task title." },
            ["due_date"] = new JsonObject { ["type"] = "string", ["description"] = "Optional due date in YYYY-MM-DD." },
            ["priority"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Low", "Normal", "High"), ["description"] = "Optional priority (default Normal)." },
            ["details"] = new JsonObject { ["type"] = "string", ["description"] = "Optional extra details." },
        },
        ["required"] = new JsonArray("title"),
    };

    public async Task<AssistantToolResult> InvokeAsync(JsonObject args, CancellationToken ct)
    {
        var title = args["title"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(title)) return new AssistantToolResult("Error: a task title is required.");

        DateOnly? due = DateOnly.TryParse(args["due_date"]?.GetValue<string>(), out var d) ? d : null;
        var priority = Enum.TryParse<TaskPriority>(args["priority"]?.GetValue<string>(), ignoreCase: true, out var p) ? p : TaskPriority.Normal;
        var details = args["details"]?.GetValue<string>();

        var task = TaskItem.Create(me.HouseholdId, me.Id, title, details, due, assigneeId: null, priority, Visibility.Household, tags: null);
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new TaskCreated(task.Id, task.HouseholdId, task.OwnerId, task.AssigneeId, task.DueDate, task.Title), ct);

        var when = due is { } dd ? $" (due {dd:yyyy-MM-dd})" : string.Empty;
        return new AssistantToolResult($"Task '{title}'{when} created.", $"Task: {title}{when}");
    }
}

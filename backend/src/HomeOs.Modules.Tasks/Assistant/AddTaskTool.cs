using System.Globalization;
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
/// Assistant tool: create a to-do task, optionally assigned to a household member. Registering this makes the
/// Tasks app <em>actionable</em> by the assistant ("napravi zadatak za Ana za sutra") with no change to the
/// kernel — it runs for the current member and publishes <see cref="TaskCreated"/> just like the normal create
/// endpoint, so the task shows up everywhere and the assignee gets the usual in-app + email notification.
/// </summary>
public sealed class AddTaskTool(ICurrentMember me, TasksDbContext db, IMemberDirectory members, IEventBus bus) : IAssistantTool
{
    public string Name => "add_task";

    public string Description => "Create a to-do task for the household (optional due date, priority, and an " +
        "assignee by name). Use this when the user wants a task/to-do, not a reminder. Assign it when the user " +
        "names a person ('za Ana', 'for Mirza') so that person gets notified.";

    public JsonObject Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["title"] = new JsonObject { ["type"] = "string", ["description"] = "The task title." },
            ["due_date"] = new JsonObject { ["type"] = "string", ["description"] = "Optional due date in YYYY-MM-DD." },
            ["priority"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Low", "Normal", "High"), ["description"] = "Optional priority (default Normal)." },
            ["assignee"] = new JsonObject { ["type"] = "string", ["description"] = "Optional household member name to assign it to (they get notified)." },
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

        // Resolve an assignee by name (case-insensitive; matches full or first name). Unknown → unassigned.
        Guid? assigneeId = null;
        string? assigneeName = null;
        var wanted = args["assignee"]?.GetValue<string>()?.Trim();
        if (!string.IsNullOrWhiteSpace(wanted))
        {
            var people = await members.GetHouseholdMembersAsync(me.HouseholdId, ct);
            var match = people.FirstOrDefault(m => m.DisplayName.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                ?? people.FirstOrDefault(m => m.DisplayName.Split(' ').FirstOrDefault()?.Equals(wanted, StringComparison.OrdinalIgnoreCase) == true)
                ?? people.FirstOrDefault(m => m.DisplayName.Contains(wanted, StringComparison.OrdinalIgnoreCase));
            if (match is not null) { assigneeId = match.Id; assigneeName = match.DisplayName; }
        }

        var task = TaskItem.Create(me.HouseholdId, me.Id, title, details, due, assigneeId, priority, Visibility.Household, tags: null);
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new TaskCreated(task.Id, task.HouseholdId, task.OwnerId, task.AssigneeId, task.DueDate, task.Title), ct);

        var when = due is { } dd ? $" (due {dd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})" : string.Empty;
        var forWhom = assigneeName is not null ? $" for {assigneeName}" : string.Empty;
        return new AssistantToolResult($"Task '{title}'{forWhom}{when} created.", $"Task: {title}{forWhom}{when}");
    }
}

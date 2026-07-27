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
        "assignee by name). Use this when the user wants a task/to-do, not a reminder. Whenever the user names " +
        "a person to be responsible — in ANY grammatical form, e.g. 'za Ana', 'zaduži Farisa', 'zadužena osoba " +
        "Faris Imamović', 'for Mirza' — set the 'assignee' field to that name so they get notified.";

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
            var match = ResolveMember(people, wanted);
            if (match is not null) { assigneeId = match.Id; assigneeName = match.DisplayName; }
        }

        var task = TaskItem.Create(me.HouseholdId, me.Id, title, details, due, assigneeId, priority, Visibility.Household, tags: null);
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new TaskCreated(task.Id, task.HouseholdId, task.OwnerId, task.AssigneeId, task.DueDate, task.Title), ct);
        if (assigneeId is { } aid)
            await bus.PublishAsync(new TaskAssigned(task.Id, task.HouseholdId, aid, me.Id, task.Title, task.DueDate), ct);

        var when = due is { } dd ? $" (due {dd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})" : string.Empty;
        var forWhom = assigneeName is not null ? $" for {assigneeName}" : string.Empty;
        return new AssistantToolResult($"Task '{title}'{forWhom}{when} created.", $"Task: {title}{forWhom}{when}");
    }

    /// <summary>
    /// Resolves a member from a free-text name, tolerating Bosnian grammatical cases ("Farisa Imamovica" →
    /// "Faris Imamović") and diacritics — the assistant often passes a declined form of the name.
    /// </summary>
    private static MemberSummary? ResolveMember(IReadOnlyList<MemberSummary> people, string wanted)
    {
        var w = Fold(wanted);
        if (w.Length == 0) return null;

        var exact = people.FirstOrDefault(m => Fold(m.DisplayName) == w);
        if (exact is not null) return exact;

        var contains = people.FirstOrDefault(m => w.Contains(Fold(m.DisplayName)) || Fold(m.DisplayName).Contains(w));
        if (contains is not null) return contains;

        // Token stem overlap — the strongest score wins (needs at least one matching name part).
        var wantedTokens = w.Split(new[] { ' ', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
        MemberSummary? best = null;
        var bestScore = 0;
        foreach (var m in people)
        {
            var nameTokens = Fold(m.DisplayName).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var score = nameTokens.Count(nt => wantedTokens.Any(wt => StemMatch(nt, wt)));
            if (score > bestScore) { bestScore = score; best = m; }
        }
        return bestScore > 0 ? best : null;
    }

    // Two name parts match if they share a long-enough prefix (so a case suffix like -a/-u/-om is ignored).
    private static bool StemMatch(string a, string b)
    {
        if (a.Length < 3 || b.Length < 3) return a == b;
        var n = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < n && a[i] == b[i]) i++;
        return i >= 3 && i >= n - 2;
    }

    // Lowercase + fold Bosnian diacritics so "Imamović" and "imamovica" compare equal at the stem.
    private static string Fold(string s) => s.Trim().ToLowerInvariant()
        .Replace("dž", "dz").Replace('ć', 'c').Replace('č', 'c').Replace('š', 's').Replace('ž', 'z').Replace('đ', 'd');
}

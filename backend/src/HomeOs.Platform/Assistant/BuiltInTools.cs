using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using HomeOs.Platform.Digest;
using HomeOs.Platform.Members;
using HomeOs.Platform.Reminders;

namespace HomeOs.Platform.Assistant;

/// <summary>Assistant tool: schedule a reminder for the current member (kernel <see cref="IReminderService"/>).</summary>
public sealed class AddReminderTool(ICurrentMember me, IReminderService reminders) : IAssistantTool
{
    public string Name => "add_reminder";
    public string Description => "Schedule a reminder for the current member.";
    public JsonObject Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["title"] = new JsonObject { ["type"] = "string", ["description"] = "What to be reminded about." },
            ["date"] = new JsonObject { ["type"] = "string", ["description"] = "Date in YYYY-MM-DD." },
            ["time"] = new JsonObject { ["type"] = "string", ["description"] = "Optional time HH:mm." },
        },
        ["required"] = new JsonArray("title", "date"),
    };

    public async Task<AssistantToolResult> InvokeAsync(JsonObject args, CancellationToken ct)
    {
        var title = args["title"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(title) || !DateOnly.TryParse(args["date"]?.GetValue<string>(), out var date))
            return new AssistantToolResult("Error: title and a valid date (YYYY-MM-DD) are required.");
        TimeOnly? time = TimeOnly.TryParse(args["time"]?.GetValue<string>(), out var tt) ? tt : null;
        // Deterministic source id from who/what/when → if the model fires the tool twice, ScheduleAsync
        // upserts instead of creating a duplicate reminder.
        var sourceId = DeterministicId($"{me.Id}|{title.ToLowerInvariant()}|{date:yyyy-MM-dd}");
        await reminders.ScheduleAsync(new ScheduledReminder(me.HouseholdId, me.Id, me.Id, title, date, time, "assistant", sourceId), ct);
        return new AssistantToolResult($"Reminder '{title}' scheduled for {date:yyyy-MM-dd}.", $"Reminder: {title} ({date:yyyy-MM-dd})");
    }

    private static Guid DeterministicId(string seed) => new(MD5.HashData(Encoding.UTF8.GetBytes(seed)));
}

/// <summary>Assistant tool: list the member's upcoming items from every registered <see cref="IUpcomingProvider"/>.</summary>
public sealed class ListUpcomingTool(ICurrentMember me, IEnumerable<IUpcomingProvider> upcoming) : IAssistantTool
{
    public string Name => "list_upcoming";
    public string Description => "List the member's upcoming tasks, bills and reminders over the next N days.";
    public JsonObject Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { ["days"] = new JsonObject { ["type"] = "integer", ["description"] = "How many days ahead (default 7)." } },
    };

    public async Task<AssistantToolResult> InvokeAsync(JsonObject args, CancellationToken ct)
    {
        var days = ReadInt(args["days"]) ?? 7;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = new List<UpcomingItem>();
        foreach (var provider in upcoming)
            items.AddRange(await provider.GetUpcomingAsync(me.HouseholdId, me.Id, today, today.AddDays(Math.Clamp(days, 1, 60)), ct));
        if (items.Count == 0) return new AssistantToolResult("Nothing coming up in that window.");
        return new AssistantToolResult(string.Join('\n', items.OrderBy(i => i.Date).Select(i => $"- {i.Date:yyyy-MM-dd} [{i.Kind}] {i.Title}")));
    }

    private static int? ReadInt(JsonNode? node)
    {
        if (node is null) return null;
        try { return (int)node.GetValue<double>(); }
        catch { try { return node.GetValue<int>(); } catch { return int.TryParse(node.ToString(), out var v) ? v : null; } }
    }
}

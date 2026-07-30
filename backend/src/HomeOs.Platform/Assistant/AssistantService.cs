using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HomeOs.Platform.Assistant;

/// <summary>One turn of the assistant conversation.</summary>
public sealed record ChatTurn(string Role, string Text);

/// <summary>The assistant's answer plus a plain-language list of any actions it took.</summary>
public sealed record AssistantReply(bool Configured, string Text, IReadOnlyList<string> Actions);

/// <summary>
/// A household assistant: natural-language questions and commands turned into real actions through an LLM's
/// tool-use. Every action is an <see cref="IAssistantTool"/> discovered from DI, so a new app becomes
/// <em>actionable</em> ("create a task", "add a note") just by registering a tool — no change here. Tools run
/// for the current member with the same auth/visibility as the rest of the app. Provider-agnostic: talks to an
/// OpenAI-compatible endpoint (Groq, Gemini, OpenRouter, Ollama — all have free options) or Anthropic,
/// selected by config; disabled until a key is set.
/// </summary>
public interface IAssistant
{
    /// <summary>Whether an assistant API key is configured (assistant disabled otherwise).</summary>
    bool Configured { get; }

    /// <summary>Answers the conversation, running tools against the current member's household.</summary>
    Task<AssistantReply> ChatAsync(IReadOnlyList<ChatTurn> history, CancellationToken ct = default);

    /// <summary>
    /// One-shot plain completion (no tools) — used for the digest's AI intro. Writes a short natural-language
    /// summary of <paramref name="content"/> in the given language. Returns empty string if unconfigured or on error.
    /// </summary>
    Task<string> SummarizeAsync(string language, string content, CancellationToken ct = default);

    /// <summary>
    /// One-shot completion with a caller-supplied system prompt and no tools — the general form of
    /// <see cref="SummarizeAsync"/>, so any app can borrow the configured model for a focused judgement
    /// (e.g. marking a written exam answer) without knowing which provider is in use.
    /// Returns an empty string when unconfigured or on error, so callers can fall back gracefully.
    /// </summary>
    Task<string> CompleteAsync(string system, string user, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AssistantService(
    IConfiguration config, IHttpClientFactory httpFactory,
    IEnumerable<IAssistantTool> tools, ILogger<AssistantService> logger)
    : IAssistant
{
    private const int MaxToolRounds = 5;

    // "openai" = any OpenAI-compatible endpoint (Groq/Gemini/OpenRouter/Ollama). "anthropic" = Claude.
    private string Provider => (config["Assistant:Provider"] ?? "openai").ToLowerInvariant();
    private string? ApiKey => config["Assistant:ApiKey"] ?? config["Anthropic:ApiKey"];
    private string Model => config["Assistant:Model"] ?? config["Anthropic:Model"]
        ?? (Provider == "anthropic" ? "claude-sonnet-5" : "llama-3.3-70b-versatile");
    private string BaseUrl => (config["Assistant:BaseUrl"] ?? "https://api.groq.com/openai/v1").TrimEnd('/');

    /// <inheritdoc />
    public bool Configured => !string.IsNullOrWhiteSpace(ApiKey);

    // Set when the provider rejects our credentials (401/403) so we can tell the user the key is wrong
    // rather than showing a vague "try again".
    private bool _authFailed;

    /// <inheritdoc />
    public async Task<AssistantReply> ChatAsync(IReadOnlyList<ChatTurn> history, CancellationToken ct = default)
    {
        if (!Configured) return new AssistantReply(false, string.Empty, []);
        _authFailed = false;
        var actions = new List<string>();
        var text = Provider == "anthropic"
            ? await RunAnthropicAsync(history, actions, ct)
            : await RunOpenAiAsync(history, actions, ct);
        return new AssistantReply(true, text, actions);
    }

    /// <inheritdoc />
    public async Task<string> SummarizeAsync(string language, string content, CancellationToken ct = default)
    {
        if (!Configured || string.IsNullOrWhiteSpace(content)) return string.Empty;
        var langName = language == "en" ? "English" : "Bosnian";
        var system = $"You write the opening line of a household's \"what's coming up\" digest email. In {langName}, " +
            "write 2-3 warm, natural sentences that summarize what's ahead and gently flag what's most time-sensitive. " +
            "No greeting, no sign-off, no markdown, no bullet points — just the sentences.";
        return await CompleteAsync(system, content, ct);
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        if (!Configured || string.IsNullOrWhiteSpace(user)) return string.Empty;
        return Provider == "anthropic"
            ? await CompleteAnthropicAsync(system, user, ct)
            : await CompleteOpenAiAsync(system, user, ct);
    }

    // ---- One-shot completions (no tools) ----

    private async Task<string> CompleteOpenAiAsync(string system, string user, CancellationToken ct)
    {
        var http = httpFactory.CreateClient("assistant");
        var body = new JsonObject
        {
            ["model"] = Model,
            ["temperature"] = 0.4,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = user },
            },
        };
        var response = await PostAsync(http, $"{BaseUrl}/chat/completions", body, bearer: true, ct);
        return (response?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? string.Empty).Trim();
    }

    private async Task<string> CompleteAnthropicAsync(string system, string user, CancellationToken ct)
    {
        var http = httpFactory.CreateClient("assistant");
        var body = new JsonObject
        {
            ["model"] = Model,
            ["max_tokens"] = 400,
            ["system"] = system,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = user }) },
            },
        };
        var response = await PostAsync(http, "https://api.anthropic.com/v1/messages", body, bearer: false, ct);
        var content = response?["content"] as JsonArray ?? [];
        return string.Concat(content.OfType<JsonObject>()
            .Where(b => b["type"]?.GetValue<string>() == "text")
            .Select(b => b["text"]?.GetValue<string>())).Trim();
    }

    private string SystemPrompt()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return $"You are the assistant inside Home OS, a household life-admin app. Today is {today:yyyy-MM-dd} ({today:dddd}). " +
            "Help the member two ways: (1) answer questions about their household and take actions with tools; " +
            "(2) explain HOW TO USE the app when asked (a friendly guide). " +
            "Use tools to act; never claim you did something you didn't. Resolve relative dates (e.g. 'next week') to concrete dates. " +
            "Reply in the same language the user writes in (Bosnian or English). Keep replies short and friendly.\n\n" +
            "HOW HOME OS WORKS (use this to guide the user):\n" +
            "- Dashboard (Today): what's due, coming up, weather, quick 'ask me' box.\n" +
            "- Tasks: to-dos with due dates, priority, assignee, tags, sub-tasks, and recurring (daily/weekly/monthly/yearly). Tick the checkbox to complete; recurring ones roll to the next date.\n" +
            "- Boards (Kanban): drag task cards across To do / Doing / Done; multiple boards per area.\n" +
            "- Calendar: month/week/day; tasks, bills and reminders show automatically; click a day to add an event.\n" +
            "- Reminders: one-off or recurring, aimed at a member; they notify in-app + email, with escalating lead-ups.\n" +
            "- Notes: notes with tags, a daily Journal mode, and links to a related task/bill/event.\n" +
            "- Finance: expenses/income by category, per-category budgets, recurring bills with due-soon alerts, a monthly who-paid summary; pick your currency in the Finance header or profile.\n" +
            "- Life admin: documents, warranties, renewals (expiry auto-creates a reminder), contacts.\n" +
            "- Shopping: shared checkable household lists.\n" +
            "- Chat: live household chat.\n" +
            "- Sharing: each item is Private, Household, or Shared with specific people; assign tasks/reminders to members.\n" +
            "- Notifications & digest: choose which emails you get per category; opt into a daily/weekly digest in your profile.\n" +
            "- Household: owners/admins invite members, set roles, edit members, rename the household. Profile: photo, language, currency, digest.\n" +
            "When explaining, point to the relevant screen by name. Keep it to the steps that matter.";
    }

    // ---- OpenAI-compatible (Groq / Gemini / OpenRouter / Ollama) ----

    private async Task<string> RunOpenAiAsync(IReadOnlyList<ChatTurn> history, List<string> actions, CancellationToken ct)
    {
        var messages = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = SystemPrompt() } };
        foreach (var turn in history)
            messages.Add(new JsonObject { ["role"] = turn.Role == "assistant" ? "assistant" : "user", ["content"] = turn.Text });

        var http = httpFactory.CreateClient("assistant");
        for (var round = 0; round < MaxToolRounds; round++)
        {
            var body = new JsonObject
            {
                ["model"] = Model,
                ["messages"] = messages.DeepClone(),
                ["tools"] = OpenAiTools(),
                ["temperature"] = 0.2,
            };
            var response = await PostAsync(http, $"{BaseUrl}/chat/completions", body, bearer: true, ct);
            var message = (response?["choices"]?[0]?["message"]) as JsonObject;
            if (message is null) return Fallback();

            var toolCalls = message["tool_calls"] as JsonArray;
            if (toolCalls is null || toolCalls.Count == 0)
                return message["content"]?.GetValue<string>()?.Trim() ?? Fallback();

            messages.Add(message.DeepClone());
            foreach (var call in toolCalls.OfType<JsonObject>())
            {
                var fn = call["function"] as JsonObject;
                var name = fn?["name"]?.GetValue<string>() ?? "";
                var args = ParseArgs(fn?["arguments"]?.GetValue<string>());
                var (result, action) = await RunToolAsync(name, args, ct);
                if (action is not null) actions.Add(action);
                messages.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = call["id"]?.GetValue<string>(), ["content"] = result });
            }
        }
        return Fallback();
    }

    // ---- Anthropic ----

    private async Task<string> RunAnthropicAsync(IReadOnlyList<ChatTurn> history, List<string> actions, CancellationToken ct)
    {
        var messages = new JsonArray();
        foreach (var turn in history)
            messages.Add(new JsonObject
            {
                ["role"] = turn.Role == "assistant" ? "assistant" : "user",
                ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = turn.Text }),
            });

        var http = httpFactory.CreateClient("assistant");
        for (var round = 0; round < MaxToolRounds; round++)
        {
            var body = new JsonObject
            {
                ["model"] = Model,
                ["max_tokens"] = 1024,
                ["system"] = SystemPrompt(),
                ["tools"] = AnthropicTools(),
                ["messages"] = messages.DeepClone(),
            };
            var response = await PostAsync(http, "https://api.anthropic.com/v1/messages", body, bearer: false, ct);
            if (response is null) return Fallback();

            var content = response["content"] as JsonArray ?? [];
            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = content.DeepClone() });

            if (response["stop_reason"]?.GetValue<string>() != "tool_use")
                return string.Concat(content.OfType<JsonObject>()
                    .Where(b => b["type"]?.GetValue<string>() == "text")
                    .Select(b => b["text"]?.GetValue<string>())).Trim();

            var toolResults = new JsonArray();
            foreach (var block in content.OfType<JsonObject>().Where(b => b["type"]?.GetValue<string>() == "tool_use"))
            {
                var (result, action) = await RunToolAsync(block["name"]?.GetValue<string>() ?? "", block["input"] as JsonObject ?? [], ct);
                if (action is not null) actions.Add(action);
                toolResults.Add(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = block["id"]?.GetValue<string>(), ["content"] = result });
            }
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = toolResults });
        }
        return Fallback();
    }

    private async Task<JsonObject?> PostAsync(HttpClient http, string url, JsonObject body, bool bearer, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(JsonNode.Parse(body.ToJsonString())) };
            if (bearer) req.Headers.Add("Authorization", $"Bearer {ApiKey}");
            else { req.Headers.Add("x-api-key", ApiKey); req.Headers.Add("anthropic-version", "2023-06-01"); }
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                    _authFailed = true;
                logger.LogWarning("Assistant provider returned {Status}: {Body}", res.StatusCode, await res.Content.ReadAsStringAsync(ct));
                return null;
            }
            return JsonNode.Parse(await res.Content.ReadAsStringAsync(ct)) as JsonObject;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Assistant call failed.");
            return null;
        }
    }

    // ---- Tools (discovered from DI; executed for the current member) ----

    private async Task<(string result, string? action)> RunToolAsync(string name, JsonObject input, CancellationToken ct)
    {
        var tool = tools.FirstOrDefault(t => t.Name == name);
        if (tool is null) return ($"Unknown tool '{name}'.", null);
        try
        {
            var r = await tool.InvokeAsync(input, ct);
            return (r.Message, r.Action);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Assistant tool {Tool} failed.", name);
            return ($"Error running '{name}'.", null);
        }
    }

    // OpenAI-style tool declarations, built from every registered tool.
    private JsonArray OpenAiTools()
    {
        var arr = new JsonArray();
        foreach (var t in tools)
            arr.Add(new JsonObject { ["type"] = "function", ["function"] = new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["parameters"] = t.Parameters } });
        return arr;
    }

    // Anthropic-style tool declarations, built from every registered tool.
    private JsonArray AnthropicTools()
    {
        var arr = new JsonArray();
        foreach (var t in tools)
            arr.Add(new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["input_schema"] = t.Parameters });
        return arr;
    }

    private static JsonObject ParseArgs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonNode.Parse(json) as JsonObject ?? []; }
        catch (System.Text.Json.JsonException) { return []; }
    }

    private string Fallback() => _authFailed
        ? "The AI key looks invalid. A household owner should check Assistant:ApiKey in settings — Groq keys start with \"gsk_\" (not \"org_\")."
        : "Sorry, I couldn't complete that right now. Please try again.";
}

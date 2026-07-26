using System.Text.Json.Nodes;

namespace HomeOs.Platform.Assistant;

/// <summary>The outcome of running an assistant tool.</summary>
/// <param name="Message">What the model sees back (fed into the next turn).</param>
/// <param name="Action">Optional human-readable summary of a change made (surfaced to the user).</param>
public sealed record AssistantToolResult(string Message, string? Action = null);

/// <summary>
/// An action the assistant can take, contributed by any module. The assistant discovers all registered tools
/// (like it discovers <c>IUpcomingProvider</c>) and offers them to the LLM — so a new app makes itself
/// <em>actionable</em> ("create a task", "add a note") just by registering one of these, with no change to the
/// kernel. Tools run for the current member, with the same auth/visibility as the rest of the app.
/// </summary>
public interface IAssistantTool
{
    /// <summary>Unique tool name the model calls (snake_case, e.g. <c>add_task</c>).</summary>
    string Name { get; }

    /// <summary>One line telling the model when to use this tool.</summary>
    string Description { get; }

    /// <summary>JSON-Schema object describing the tool's parameters (fresh instance each get).</summary>
    JsonObject Parameters { get; }

    /// <summary>Executes the tool with the model-provided <paramref name="args"/>.</summary>
    Task<AssistantToolResult> InvokeAsync(JsonObject args, CancellationToken ct);
}

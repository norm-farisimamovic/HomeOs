using HomeOs.Platform.Assistant;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HomeOs.Platform.Features.Assistant;

/// <summary>The household AI assistant — natural-language questions and commands over the app.</summary>
public static class AssistantEndpoints
{
    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assistant").RequireAuthorization().WithTags("Assistant");

        group.MapGet("/status", (IAssistant assistant) => Results.Ok(new { configured = assistant.Configured }))
            .WithName("AssistantStatus");

        group.MapPost("/chat", async (ChatRequest req, IAssistant assistant, CancellationToken ct) =>
        {
            var history = (req.Messages ?? [])
                .Where(m => !string.IsNullOrWhiteSpace(m.Text))
                .TakeLast(20)
                .Select(m => new ChatTurn(m.Role, m.Text))
                .ToList();
            if (history.Count == 0) return Results.BadRequest();

            // No echo notification here: the chat reply already confirms what was done, open screens refresh
            // via the audit "changed" broadcast, and the real notifications still fire (the assignee gets a
            // task-assigned alert; a reminder alerts when it's due) — so notifying the asker too would double up.
            var reply = await assistant.ChatAsync(history, ct);
            return Results.Ok(new ChatResponse(reply.Configured, reply.Text, reply.Actions));
        }).WithName("AssistantChat");

        return app;
    }
}

/// <summary>One message in the client's conversation (role: <c>user</c> | <c>assistant</c>).</summary>
public sealed record ChatMessage(string Role, string Text);
/// <summary>Chat request — the running conversation.</summary>
public sealed record ChatRequest(IReadOnlyList<ChatMessage>? Messages);
/// <summary>Chat response.</summary>
public sealed record ChatResponse(bool Configured, string Text, IReadOnlyList<string> Actions);

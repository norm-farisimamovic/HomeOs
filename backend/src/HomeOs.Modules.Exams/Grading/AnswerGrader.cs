using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomeOs.Modules.Exams.Bank;
using HomeOs.Platform.Assistant;
using Microsoft.Extensions.Logging;

namespace HomeOs.Modules.Exams.Grading;

/// <summary>The verdict on one answer.</summary>
/// <param name="Points">Points awarded, never above the question's maximum.</param>
/// <param name="Correct">Whether the answer counts as correct (full marks).</param>
/// <param name="Feedback">One sentence explaining the mark, in the candidate's language.</param>
/// <param name="AiGraded">True when an AI examiner produced the verdict.</param>
/// <param name="Graded">
/// False when the answer could not be marked at all (no AI examiner available). Such a question is left
/// out of the score entirely — the candidate simply gets the model answer to read — so a missing key can
/// never block finishing a paper or drag the grade down.
/// </param>
public sealed record Verdict(decimal Points, bool Correct, string? Feedback, bool AiGraded, bool Graded = true);

/// <summary>A written answer waiting to be marked.</summary>
/// <param name="Question">The bank question, including its model answer.</param>
/// <param name="Given">What the candidate wrote.</param>
public sealed record OpenSubmission(BankQuestion Question, string Given);

/// <summary>
/// Marks exam answers. Multiple-choice is decided **locally** by comparing option sets — the correct
/// answers ship with the app, so no network and no AI is involved. Written answers are marked on
/// <em>meaning</em> by an AI examiner reading them against the model answer, using whichever provider is
/// configured (<c>Assistant:*</c> — the same one the household assistant uses). When no examiner is
/// available, written answers are **left ungraded** rather than guessed at: they drop out of the score and
/// the candidate is simply shown the model answer.
/// </summary>
public sealed class AnswerGrader(IAssistant assistant, ILogger<AnswerGrader> logger)
{
    /// <summary>Whether an AI examiner is available (written answers are skipped when it is not).</summary>
    public bool AiAvailable => assistant.Configured;

    /// <summary>Marks a multiple-choice answer: every correct option, and no incorrect one.</summary>
    public static Verdict GradeChoice(BankQuestion question, string given)
    {
        var picked = ParseIndices(given);
        var correct = question.Correct.ToHashSet();
        var isCorrect = picked.Count > 0 && picked.SetEquals(correct);
        return new Verdict(isCorrect ? question.MaxPoints : 0m, isCorrect, null, false);
    }

    /// <summary>
    /// Marks every written answer in one go. Unanswered ones score zero without troubling the model; the rest
    /// go to the AI examiner in a single request (cheap and fast). Anything the examiner can't mark — no key
    /// configured, provider down, unusable reply — comes back <see cref="Verdict.Graded"/> = false so the
    /// question is dropped from the score instead of being guessed at.
    /// </summary>
    /// <param name="submissions">The written answers on the paper.</param>
    /// <param name="language">Culture code for the feedback wording (<c>bs</c> or <c>en</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyDictionary<string, Verdict>> GradeOpenAsync(
        IReadOnlyList<OpenSubmission> submissions, string language, CancellationToken ct = default)
    {
        var verdicts = new Dictionary<string, Verdict>(StringComparer.OrdinalIgnoreCase);
        var answered = new List<OpenSubmission>();

        foreach (var s in submissions)
        {
            // A blank answer needs no examiner — but it only counts as zero when there *is* one, otherwise
            // the whole question sits outside the score like every other unmarkable written answer.
            if (string.IsNullOrWhiteSpace(s.Given))
                verdicts[s.Question.Id] = assistant.Configured
                    ? new Verdict(0m, false, Text(language, "empty"), false)
                    : Ungraded(language);
            else
                answered.Add(s);
        }

        if (answered.Count == 0) return verdicts;

        if (assistant.Configured)
        {
            var aiVerdicts = await GradeWithAiAsync(answered, language, ct);
            foreach (var (id, verdict) in aiVerdicts) verdicts[id] = verdict;
        }

        foreach (var s in answered.Where(s => !verdicts.ContainsKey(s.Question.Id)))
            verdicts[s.Question.Id] = Ungraded(language);

        return verdicts;
    }

    /// <summary>The verdict for a written answer no examiner could mark: outside the score, model answer shown.</summary>
    private static Verdict Ungraded(string language) =>
        new(0m, false, Text(language, "ungraded"), false, Graded: false);

    // ---- AI examiner ----

    private async Task<Dictionary<string, Verdict>> GradeWithAiAsync(
        IReadOnlyList<OpenSubmission> submissions, string language, CancellationToken ct)
    {
        var result = new Dictionary<string, Verdict>(StringComparer.OrdinalIgnoreCase);
        var feedbackLanguage = language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "English" : "Bosnian";

        var system =
            "You are a strict but fair examiner marking written answers on a Bosnian legal exam. " +
            "Mark on MEANING, not wording: the candidate does not have to quote the law, a correct summary in " +
            "their own words earns full marks. Ignore spelling, grammar and diacritics. " +
            "For each answer give points: 2 = substantially correct (the key elements are there), " +
            "1 = partly correct (some key elements missing or one clear error), 0 = wrong, empty or off-topic. " +
            $"Write one short sentence of feedback in {feedbackLanguage} saying what was missing or confirming it is correct. " +
            "Reply with ONLY a JSON array, no prose and no code fences: " +
            "[{\"id\":\"<question id>\",\"points\":<0|1|2>,\"feedback\":\"<one sentence>\"}]";

        var payload = new JsonArray();
        foreach (var s in submissions)
            payload.Add(new JsonObject
            {
                ["id"] = s.Question.Id,
                ["question"] = s.Question.Text,
                ["model_answer"] = s.Question.Answer ?? string.Empty,
                ["key_points"] = new JsonArray([.. s.Question.Keywords.Select(k => (JsonNode?)JsonValue.Create(k))]),
                ["candidate_answer"] = s.Given,
            });

        try
        {
            var reply = await assistant.CompleteAsync(system, payload.ToJsonString(), ct);
            if (string.IsNullOrWhiteSpace(reply)) return result;

            foreach (var node in ParseArray(reply).OfType<JsonObject>())
            {
                var id = node["id"]?.GetValue<string>();
                if (id is null) continue;
                var submission = submissions.FirstOrDefault(s => string.Equals(s.Question.Id, id, StringComparison.OrdinalIgnoreCase));
                if (submission is null) continue;

                var raw = node["points"]?.GetValue<double>() ?? 0;
                var points = Math.Clamp((decimal)raw, 0m, submission.Question.MaxPoints);
                var feedback = node["feedback"]?.GetValue<string>();
                result[id] = new Verdict(points, points >= submission.Question.MaxPoints, feedback, true);
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            // A malformed reply is not an exam failure — the key-term marker picks these up.
            logger.LogWarning(ex, "AI examiner returned an unusable reply; falling back to key-term marking.");
        }

        return result;
    }

    /// <summary>Pulls the JSON array out of a reply that may be wrapped in prose or a code fence.</summary>
    private static JsonArray ParseArray(string reply)
    {
        var text = reply.Trim();
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start) return [];
        return JsonNode.Parse(text[start..(end + 1)]) as JsonArray ?? [];
    }

    // ---- Text helpers ----

    /// <summary>Reads "0,2" (the wire format for picked options) into a set of indices.</summary>
    public static HashSet<int> ParseIndices(string given) =>
    [
        .. given.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : -1)
            .Where(i => i >= 0),
    ];

    private static string Text(string language, string key)
    {
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "empty" => en ? "No answer given." : "Odgovor nije upisan.",
            "ungraded" => en
                ? "Not graded — no AI examiner was available, so this question is left out of the score. Compare your answer with the model answer."
                : "Nije ocijenjeno — AI ispitivač nije bio dostupan, pa se pitanje ne računa u rezultat. Uporedi svoj odgovor sa tačnim.",
            _ => en ? "Compare your answer with the model answer." : "Uporedi svoj odgovor sa tačnim odgovorom.",
        };
    }
}

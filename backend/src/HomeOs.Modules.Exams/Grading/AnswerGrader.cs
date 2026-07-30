using System.Globalization;
using System.Text;
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
public sealed record Verdict(decimal Points, bool Correct, string? Feedback, bool AiGraded);

/// <summary>A written answer waiting to be marked.</summary>
/// <param name="Question">The bank question, including its model answer.</param>
/// <param name="Given">What the candidate wrote.</param>
public sealed record OpenSubmission(BankQuestion Question, string Given);

/// <summary>
/// Marks exam answers. Multiple-choice is decided by comparing option sets. Written answers are marked on
/// <em>meaning</em>: an AI examiner reads them against the model answer when one is configured
/// (<c>Assistant:*</c> — the same provider the household assistant uses), and a key-term overlap check takes over
/// when it is not, so the exam always produces a mark even offline.
/// </summary>
public sealed class AnswerGrader(IAssistant assistant, ILogger<AnswerGrader> logger)
{
    // A written answer must cover this share of the model answer's key terms for full / half marks.
    private const double FullCredit = 0.7;
    private const double HalfCredit = 0.4;

    /// <summary>Whether an AI examiner is available (otherwise written answers are marked by key terms).</summary>
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
    /// Marks every written answer in one go. Unanswered ones score zero without troubling the model; the rest go
    /// to the AI examiner in a single request (cheap and fast), falling back to key terms on any failure.
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
            if (string.IsNullOrWhiteSpace(s.Given))
                verdicts[s.Question.Id] = new Verdict(0m, false, Text(language, "empty"), false);
            else
                answered.Add(s);
        }

        if (answered.Count == 0) return verdicts;

        if (assistant.Configured)
        {
            var aiVerdicts = await GradeWithAiAsync(answered, language, ct);
            foreach (var (id, verdict) in aiVerdicts) verdicts[id] = verdict;
        }

        // Anything the AI didn't (or couldn't) mark still gets a fair mark from key terms.
        foreach (var s in answered.Where(s => !verdicts.ContainsKey(s.Question.Id)))
            verdicts[s.Question.Id] = GradeByKeywords(s.Question, s.Given, language);

        return verdicts;
    }

    /// <summary>
    /// Marks a written answer without an AI: how much of the model answer's key vocabulary it covers. Words are
    /// compared on their stems because Bosnian inflects heavily ("rješenje" / "rješenjem" / "rješenja").
    /// </summary>
    public static Verdict GradeByKeywords(BankQuestion question, string given, string language)
    {
        var keywords = question.Keywords.Count > 0
            ? question.Keywords
            : SignificantWords(question.Answer ?? string.Empty).Take(8).ToList();
        if (keywords.Count == 0) return new Verdict(0m, false, Text(language, "manual"), false);

        var answerStems = Stems(given);
        var hits = keywords.Count(k => Stems(k).All(answerStems.Contains));
        var coverage = (double)hits / keywords.Count;

        // A one-word answer that happens to contain a key term shouldn't score full marks.
        if (answerStems.Count < 3) coverage = Math.Min(coverage, HalfCredit);

        return coverage switch
        {
            >= FullCredit => new Verdict(question.MaxPoints, true, Text(language, "good"), false),
            >= HalfCredit => new Verdict(Math.Round(question.MaxPoints / 2m, 2), false, Text(language, "partial"), false),
            _ => new Verdict(0m, false, Text(language, "weak"), false),
        };
    }

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

    /// <summary>Lower-cases, strips diacritics and punctuation so "Rješenje!" and "rjesenje" compare equal.</summary>
    private static string Normalize(string text)
    {
        var lowered = text.ToLowerInvariant()
            .Replace("đ", "dj", StringComparison.Ordinal)
            .Replace("ž", "z", StringComparison.Ordinal)
            .Replace("š", "s", StringComparison.Ordinal)
            .Replace("č", "c", StringComparison.Ordinal)
            .Replace("ć", "c", StringComparison.Ordinal);

        var builder = new StringBuilder(lowered.Length);
        foreach (var ch in lowered.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return builder.ToString();
    }

    /// <summary>Words worth matching on — normalized, de-duplicated, without one/two-letter noise.</summary>
    private static IEnumerable<string> SignificantWords(string text) =>
        Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).Distinct();

    /// <summary>Stems of every significant word (first 5 characters), the unit key terms are matched on.</summary>
    private static HashSet<string> Stems(string text) =>
        [.. SignificantWords(text).Select(w => w.Length > 5 ? w[..5] : w)];

    private static string Text(string language, string key)
    {
        var en = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        return key switch
        {
            "empty" => en ? "No answer given." : "Odgovor nije upisan.",
            "good" => en ? "Covers the key points." : "Odgovor pokriva ključne elemente.",
            "partial" => en ? "Partly correct — some key points are missing." : "Djelimično tačno — nedostaju neki ključni elementi.",
            "weak" => en ? "The key points of the model answer are missing." : "Nedostaju ključni elementi tačnog odgovora.",
            _ => en ? "Compare your answer with the model answer." : "Uporedite svoj odgovor sa tačnim odgovorom.",
        };
    }
}

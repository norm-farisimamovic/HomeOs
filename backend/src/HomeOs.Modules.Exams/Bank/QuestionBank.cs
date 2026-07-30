using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeOs.Modules.Exams.Bank;

/// <summary>How a question is answered — and therefore how it is marked.</summary>
public enum QuestionType
{
    /// <summary>Exactly one correct option.</summary>
    Single,
    /// <summary>Two or more correct options; all of them and nothing else.</summary>
    Multi,
    /// <summary>Free text, marked on meaning rather than wording.</summary>
    Open,
}

/// <summary>
/// One question from the bank. Immutable reference data loaded from the embedded JSON files — never stored in
/// the database, so the bank can grow with a new release without a migration.
/// </summary>
public sealed record BankQuestion
{
    /// <summary>Stable id, unique across the whole bank (e.g. <c>zup-014</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Law code this question belongs to (see <see cref="LawCatalog"/>).</summary>
    public required string Law { get; init; }

    /// <summary>Article(s) the answer comes from, for the review screen (e.g. "Član 68").</summary>
    public string? Article { get; init; }

    /// <summary>Chapter/topic grouping used by study mode (e.g. "Osnovna načela").</summary>
    public string? Topic { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required QuestionType Type { get; init; }

    /// <summary>The question as it is put to the candidate.</summary>
    public required string Text { get; init; }

    /// <summary>Options for <see cref="QuestionType.Single"/>/<see cref="QuestionType.Multi"/>; empty for open ones.</summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>Indices into <see cref="Options"/> that are correct.</summary>
    public IReadOnlyList<int> Correct { get; init; } = [];

    /// <summary>Model answer for open questions — the yardstick for marking, not a required wording.</summary>
    public string? Answer { get; init; }

    /// <summary>Key terms an acceptable open answer should touch; used to mark when no AI examiner is configured.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Why the answer is what it is — shown after the exam.</summary>
    public string? Explanation { get; init; }

    /// <summary>Points a fully correct answer is worth (open questions are worth more because they ask for more).</summary>
    public decimal MaxPoints => Type == QuestionType.Open ? 2m : 1m;
}

/// <summary>A law covered by the bank, with the counts needed to build an exam.</summary>
public sealed record LawInfo(string Code, string Title, string ShortTitle, string Gazette, int Total, int Choice, int Open);

/// <summary>The laws in the bank, in the order they are shown. Titles are legal names, so they are not translated.</summary>
public static class LawCatalog
{
    /// <summary>Law code → (official title, short label, official gazette reference).</summary>
    public static readonly IReadOnlyDictionary<string, (string Title, string Short, string Gazette)> All =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["zup"] = ("Zakon o upravnom postupku Federacije BiH", "ZUP FBiH",
                "\"Službene novine FBiH\", br. 2/98, 48/99 i 61/22"),
            ["znr"] = ("Zakon o zaštiti na radu Federacije BiH", "Zaštita na radu",
                "\"Službene novine FBiH\", br. 79/20"),
            ["insp"] = ("Zakon o inspekcijama Tuzlanskog kantona", "Inspekcije TK",
                "\"Sl. novine TK\", br. 12/20, 1/22, 5/22 - ispr. i 11/22"),
            ["ds"] = ("Zakon o državnoj službi u Tuzlanskom kantonu", "Državna služba TK",
                "\"Sl. novine TK\", broj 11/24 - prečišćen tekst"),
        };
}

/// <summary>
/// The exam question bank: reference data read once from the module's embedded JSON files. Registered as a
/// singleton so the JSON is parsed a single time for the life of the process.
/// </summary>
public sealed class QuestionBank
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, BankQuestion> _byId;
    private readonly List<BankQuestion> _all;

    /// <summary>Loads every <c>Bank/Data/*.json</c> file embedded in this assembly.</summary>
    public QuestionBank()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var questions = new List<BankQuestion>();

        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var parsed = JsonSerializer.Deserialize<List<BankQuestion>>(stream, JsonOptions) ?? [];
            questions.AddRange(parsed);
        }

        // A duplicate id would silently overwrite a question, so keep the first and drop repeats deterministically.
        _all = questions.GroupBy(q => q.Id).Select(g => g.First()).ToList();
        _byId = _all.ToDictionary(q => q.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Every question in the bank.</summary>
    public IReadOnlyList<BankQuestion> All => _all;

    /// <summary>Looks a question up by its stable id.</summary>
    public BankQuestion? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>The laws covered, with question counts — what the exam-setup screen offers.</summary>
    public IReadOnlyList<LawInfo> Laws() =>
    [
        .. LawCatalog.All
            .Select(l =>
            {
                var qs = _all.Where(q => string.Equals(q.Law, l.Key, StringComparison.OrdinalIgnoreCase)).ToList();
                return new LawInfo(l.Key, l.Value.Title, l.Value.Short, l.Value.Gazette,
                    qs.Count, qs.Count(q => q.Type != QuestionType.Open), qs.Count(q => q.Type == QuestionType.Open));
            })
            .Where(l => l.Total > 0),
    ];

    /// <summary>
    /// Draws a paper: <paramref name="count"/> questions from the chosen <paramref name="laws"/>, spread as evenly
    /// as the bank allows so no single law dominates, and shuffled.
    /// </summary>
    /// <param name="laws">Law codes to draw from; empty means the whole bank.</param>
    /// <param name="count">How many questions to place on the paper.</param>
    /// <param name="mode"><c>choice</c> (only multiple-choice), <c>open</c> (only written) or <c>mixed</c>.</param>
    /// <param name="random">Randomness source — injected so tests can make a draw repeatable.</param>
    public IReadOnlyList<BankQuestion> Draw(IReadOnlyCollection<string> laws, int count, string mode, Random random)
    {
        var pool = _all.AsEnumerable();
        if (laws.Count > 0)
            pool = pool.Where(q => laws.Contains(q.Law, StringComparer.OrdinalIgnoreCase));
        pool = mode switch
        {
            "choice" => pool.Where(q => q.Type != QuestionType.Open),
            "open" => pool.Where(q => q.Type == QuestionType.Open),
            _ => pool,
        };

        // Round-robin across laws so a 20-question paper over 4 laws asks ~5 from each.
        var buckets = pool
            .GroupBy(q => q.Law, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Queue<BankQuestion>(g.OrderBy(_ => random.Next())))
            .ToList();

        var drawn = new List<BankQuestion>(count);
        while (drawn.Count < count && buckets.Any(b => b.Count > 0))
            foreach (var bucket in buckets.Where(b => b.Count > 0))
            {
                if (drawn.Count == count) break;
                drawn.Add(bucket.Dequeue());
            }

        return [.. drawn.OrderBy(_ => random.Next())];
    }

    /// <summary>
    /// Study-mode listing: the questions of the chosen laws in reading order, optionally narrowed by a
    /// search term. An empty <paramref name="laws"/> means the whole bank (the "mixed" case).
    /// </summary>
    public IReadOnlyList<BankQuestion> Study(IReadOnlyCollection<string>? laws, string? query)
    {
        var pool = _all.AsEnumerable();
        if (laws is { Count: > 0 })
            pool = pool.Where(q => laws.Contains(q.Law, StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query))
            pool = pool.Where(q =>
                q.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (q.Answer?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (q.Topic?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (q.Article?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || q.Options.Any(o => o.Contains(query, StringComparison.OrdinalIgnoreCase)));
        return [.. pool.OrderBy(q => q.Id, StringComparer.OrdinalIgnoreCase)];
    }
}

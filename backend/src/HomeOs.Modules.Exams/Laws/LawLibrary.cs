using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using HomeOs.Modules.Exams.Bank;

namespace HomeOs.Modules.Exams.Laws;

/// <summary>One article of a law, as it reads in the official text.</summary>
/// <param name="Key">Article number, letter suffixes included (<c>17a</c>, <c>95d</c>).</param>
/// <param name="Label">How the article is cited ("Član 17a").</param>
/// <param name="Title">The article's own heading, when the law gives one.</param>
/// <param name="Chapter">The part/chapter it sits under, for orientation.</param>
/// <param name="Text">The article's paragraphs, newline-separated.</param>
public sealed record LawArticle(string Key, string Label, string Title, string Chapter, string Text);

/// <summary>A whole law: its metadata plus every article.</summary>
public sealed record LawText(string Code, string Title, string ShortTitle, string Gazette, IReadOnlyList<LawArticle> Articles);

/// <summary>
/// The full text of the four laws the question bank is drawn from, so a question's article citation can be
/// read in place rather than looked up on some website. Reference data: embedded JSON parsed once into
/// memory at startup, exactly like <see cref="QuestionBank"/>.
/// </summary>
public sealed class LawLibrary
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Pulls the article number out of a citation like "Član 60", "Čl. 12. i 13." or "Član 95a".</summary>
    private static readonly Regex CitationKey = new(@"(?:Član|Članak|Čl)\.?\s*(\d+[a-zA-Z]?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly Dictionary<string, LawText> _laws;

    /// <summary>Loads every <c>Laws/Data/law-*.json</c> file embedded in this assembly.</summary>
    public LawLibrary()
    {
        var assembly = Assembly.GetExecutingAssembly();
        _laws = new Dictionary<string, LawText>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.Contains(".Laws.Data.", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var doc = JsonSerializer.Deserialize<LawDocument>(stream, JsonOptions);
            if (doc is null || !LawCatalog.All.TryGetValue(doc.Code, out var meta)) continue;
            _laws[doc.Code] = new LawText(doc.Code, meta.Title, meta.Short, meta.Gazette, doc.Articles);
        }
    }

    /// <summary>The laws on the shelf, in catalogue order.</summary>
    public IReadOnlyList<LawText> All =>
        [.. LawCatalog.All.Keys.Where(_laws.ContainsKey).Select(code => _laws[code])];

    /// <summary>One law by its code, or <c>null</c> when it isn't on the shelf.</summary>
    public LawText? Find(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : _laws.GetValueOrDefault(code);

    /// <summary>One article by law code and article key.</summary>
    public LawArticle? Article(string? code, string? key) =>
        Find(code)?.Articles.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The article key a question's citation points at, so the UI can turn "Član 60" into a link. Returns
    /// <c>null</c> when the citation names no article, or names one this law doesn't have.
    /// </summary>
    public string? KeyFor(string? lawCode, string? citation)
    {
        if (string.IsNullOrWhiteSpace(citation)) return null;
        var match = CitationKey.Match(citation);
        if (!match.Success) return null;
        return Article(lawCode, match.Groups[1].Value)?.Key;
    }

    /// <summary>Articles of a law, optionally narrowed to those matching <paramref name="query"/>.</summary>
    public IReadOnlyList<LawArticle> Search(string code, string? query)
    {
        var law = Find(code);
        if (law is null) return [];
        if (string.IsNullOrWhiteSpace(query)) return law.Articles;

        return [.. law.Articles.Where(a =>
            a.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
            || a.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || a.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
            || a.Chapter.Contains(query, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>Shape of the embedded JSON file.</summary>
    private sealed record LawDocument(string Code, List<LawArticle> Articles);
}

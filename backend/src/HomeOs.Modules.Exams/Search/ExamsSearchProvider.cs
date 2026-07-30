using System.Security.Cryptography;
using System.Text;
using HomeOs.Modules.Exams.Bank;
using HomeOs.Platform.Search;

namespace HomeOs.Modules.Exams.Search;

/// <summary>
/// Contributes the question bank to global search, so typing "žalba" in ⌘K surfaces the questions (and the
/// article) that cover it and jumps straight into study mode.
/// </summary>
public sealed class ExamsSearchProvider(QuestionBank bank) : ISearchProvider
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, CancellationToken ct = default)
    {
        IReadOnlyList<SearchHit> hits =
        [
            .. bank.Study(null, query).Take(5).Select(q => new SearchHit(
                "exams", StableId(q.Id), q.Text,
                string.Join(" · ", new[] { q.LawShort(), q.Article }.Where(s => !string.IsNullOrWhiteSpace(s))),
                $"/exams?tab=study&law={q.Law}&q={Uri.EscapeDataString(q.Id)}")),
        ];
        return Task.FromResult(hits);
    }

    /// <summary>
    /// Search hits are keyed by <see cref="Guid"/>, but bank questions have string ids — derive a stable Guid so
    /// the same question always yields the same key (React list keys, de-duplication).
    /// </summary>
    private static Guid StableId(string questionId) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes(questionId)));
}

/// <summary>Small readability helper for the search subtitle.</summary>
internal static class BankQuestionExtensions
{
    /// <summary>The law's short label (falls back to the raw code).</summary>
    public static string LawShort(this BankQuestion q) =>
        LawCatalog.All.TryGetValue(q.Law, out var meta) ? meta.Short : q.Law;
}

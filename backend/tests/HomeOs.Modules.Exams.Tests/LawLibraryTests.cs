using System.Text.RegularExpressions;
using HomeOs.Modules.Exams.Bank;
using HomeOs.Modules.Exams.Laws;
using Shouldly;

namespace HomeOs.Modules.Exams.Tests;

/// <summary>
/// The law texts are hand-parsed from the official gazette texts, so these guard the shape a reader depends
/// on — and, crucially, that **every** question's article citation resolves to an article that actually exists.
/// </summary>
public class LawLibraryTests
{
    private static readonly LawLibrary Library = new();
    private static readonly QuestionBank Bank = new();

    [Fact]
    public void Every_law_in_the_catalogue_has_its_text()
    {
        Library.All.Select(l => l.Code).ShouldBe(LawCatalog.All.Keys, ignoreOrder: true);
        Library.All.ShouldAllBe(l => l.Articles.Count > 50);
        Library.All.ShouldAllBe(l => !string.IsNullOrWhiteSpace(l.Title) && !string.IsNullOrWhiteSpace(l.Gazette));
    }

    [Fact]
    public void Articles_are_labelled_uniquely_and_never_empty()
    {
        foreach (var law in Library.All)
        {
            law.Articles.Select(a => a.Key).Distinct().Count().ShouldBe(law.Articles.Count, law.Code);
            law.Articles.ShouldAllBe(a => a.Text.Length > 20);
            law.Articles.ShouldAllBe(a => a.Label.StartsWith("Član "));
            law.Articles.ShouldAllBe(a => Regex.IsMatch(a.Key, "^[0-9]+[a-z]?$"));
        }
    }

    [Fact]
    public void An_article_reads_as_the_official_text_does()
    {
        var article = Library.Article("zup", "216");

        article.ShouldNotBeNull();
        article!.Label.ShouldBe("Član 216");
        article.Title.ShouldContain("rješenj", Case.Insensitive);
        // The 30/60/15-day deadlines are the substance of this article.
        article.Text.ShouldContain("30 dana");
        article.Text.ShouldContain("60 dana");
        article.Text.ShouldContain("15 dana");
    }

    [Fact]
    public void Articles_with_a_letter_suffix_survive_parsing()
    {
        Library.Article("zup", "17a").ShouldNotBeNull();   // jedinstveno upravno mjesto
        Library.Article("insp", "95a").ShouldNotBeNull();  // izuzeće kantonalnog inspektora
        Library.Article("insp", "57a").ShouldNotBeNull();
    }

    [Fact]
    public void Every_question_citation_resolves_to_a_real_article()
    {
        var unresolved = Bank.All
            .Where(q => Library.KeyFor(q.Law, q.Article) is null)
            .Select(q => $"{q.Id} → {q.Article}")
            .ToList();

        unresolved.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Član 60", "60")]
    [InlineData("Član 60 stav 2", "60")]
    [InlineData("Čl. 12. i 13.", "12")]      // a multi-article citation opens the first one
    [InlineData("Član 17a", "17a")]
    [InlineData("Član 205 i 206", "205")]
    public void A_citation_points_at_its_first_article(string citation, string expected) =>
        Library.KeyFor("zup", citation).ShouldBe(expected);

    [Fact]
    public void A_citation_that_names_no_real_article_resolves_to_nothing()
    {
        Library.KeyFor("zup", "Član 9999").ShouldBeNull();
        Library.KeyFor("zup", "Ustav BiH").ShouldBeNull();
        Library.KeyFor("nosuchlaw", "Član 1").ShouldBeNull();
        Library.KeyFor("zup", null).ShouldBeNull();
    }

    [Fact]
    public void Search_narrows_a_law_to_the_articles_that_mention_a_term()
    {
        var hits = Library.Search("znr", "povjerenik");

        hits.ShouldNotBeEmpty();
        hits.Count.ShouldBeLessThan(Library.Find("znr")!.Articles.Count);
        hits.ShouldAllBe(a => a.Text.Contains("ovjerenik") || a.Title.Contains("ovjerenik"));
        Library.Search("znr", null).Count.ShouldBe(Library.Find("znr")!.Articles.Count);
        Library.Search("znr", "zzzzz-nepostojeci").ShouldBeEmpty();
    }
}

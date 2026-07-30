using HomeOs.Modules.Exams.Bank;
using Shouldly;

namespace HomeOs.Modules.Exams.Tests;

/// <summary>
/// The bank is reference data typed by hand, so these guard it the way a compiler can't: every question
/// has to be answerable and markable. A malformed question would otherwise only surface mid-exam.
/// </summary>
public class QuestionBankTests
{
    private static readonly QuestionBank Bank = new();

    [Fact]
    public void Bank_loads_questions_for_every_law()
    {
        Bank.All.Count.ShouldBeGreaterThan(400);
        Bank.Laws().Select(l => l.Code).ShouldBe(["zup", "znr", "insp", "ds"], ignoreOrder: true);
        Bank.Laws().ShouldAllBe(l => l.Total > 50);
    }

    [Fact]
    public void Question_ids_are_unique()
    {
        var duplicates = Bank.All.GroupBy(q => q.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        duplicates.ShouldBeEmpty();
    }

    [Fact]
    public void Choice_questions_are_answerable()
    {
        foreach (var q in Bank.All.Where(q => q.Type != QuestionType.Open))
        {
            q.Options.Count.ShouldBeGreaterThanOrEqualTo(2, q.Id);
            q.Correct.ShouldNotBeEmpty(q.Id);
            q.Correct.ShouldAllBe(i => i >= 0 && i < q.Options.Count, q.Id);
            q.Correct.Distinct().Count().ShouldBe(q.Correct.Count, q.Id);
            if (q.Type == QuestionType.Single) q.Correct.Count.ShouldBe(1, q.Id);
            else q.Correct.Count.ShouldBeGreaterThan(1, q.Id);
        }
    }

    [Fact]
    public void Written_questions_carry_a_model_answer_and_key_terms()
    {
        foreach (var q in Bank.All.Where(q => q.Type == QuestionType.Open))
        {
            q.Answer.ShouldNotBeNullOrWhiteSpace(q.Id);
            q.Keywords.ShouldNotBeEmpty(q.Id);
            q.Options.ShouldBeEmpty(q.Id);
        }
    }

    [Fact]
    public void Every_question_names_its_law_and_article()
    {
        foreach (var q in Bank.All)
        {
            LawCatalog.All.ContainsKey(q.Law).ShouldBeTrue(q.Id);
            q.Text.ShouldNotBeNullOrWhiteSpace(q.Id);
            q.Article.ShouldNotBeNullOrWhiteSpace(q.Id);
        }
    }

    [Fact]
    public void Draw_spreads_the_paper_across_the_chosen_laws()
    {
        var drawn = Bank.Draw(["zup", "znr"], 20, "mixed", new Random(7));

        drawn.Count.ShouldBe(20);
        drawn.Select(q => q.Id).Distinct().Count().ShouldBe(20);
        drawn.ShouldAllBe(q => q.Law == "zup" || q.Law == "znr");
        // Round-robin across two laws should give roughly half each — never all of one.
        drawn.Count(q => q.Law == "zup").ShouldBe(10);
    }

    [Fact]
    public void Draw_honours_the_requested_question_mix()
    {
        Bank.Draw([], 15, "choice", new Random(1)).ShouldAllBe(q => q.Type != QuestionType.Open);
        Bank.Draw([], 15, "open", new Random(1)).ShouldAllBe(q => q.Type == QuestionType.Open);
    }

    [Fact]
    public void Draw_never_returns_more_than_the_pool_holds()
    {
        var open = Bank.All.Count(q => q.Type == QuestionType.Open);
        Bank.Draw([], 500, "open", new Random(3)).Count.ShouldBe(open);
    }

    [Fact]
    public void Study_filters_by_law_and_search_term()
    {
        Bank.Study(["znr"], null).ShouldAllBe(q => q.Law == "znr");
        Bank.Study(null, "žalba").ShouldNotBeEmpty();
        Bank.Study(null, "zzzzz-nepostojeci-pojam").ShouldBeEmpty();
    }

    [Fact]
    public void Study_can_mix_several_laws_at_once()
    {
        var mixed = Bank.Study(["znr", "ds"], null);

        mixed.ShouldAllBe(q => q.Law == "znr" || q.Law == "ds");
        mixed.Count.ShouldBe(Bank.Study(["znr"], null).Count + Bank.Study(["ds"], null).Count);
        // No laws at all means the whole bank — the "mixed" case.
        Bank.Study([], null).Count.ShouldBe(Bank.All.Count);
    }
}

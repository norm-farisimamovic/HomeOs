using HomeOs.Modules.Exams.Bank;
using HomeOs.Modules.Exams.Grading;
using Shouldly;

namespace HomeOs.Modules.Exams.Tests;

/// <summary>Marking rules: choice questions by set equality, written answers on meaning, then the 1–5 scale.</summary>
public class GradingTests
{
    private static BankQuestion Choice(QuestionType type, params int[] correct) => new()
    {
        Id = "t-1", Law = "zup", Type = type, Text = "?",
        Options = ["a", "b", "c", "d"], Correct = correct,
    };

    private static BankQuestion Written(string answer, params string[] keywords) => new()
    {
        Id = "t-2", Law = "zup", Type = QuestionType.Open, Text = "?", Answer = answer, Keywords = keywords,
    };

    [Theory]
    [InlineData("1", true)]
    [InlineData("2", false)]
    [InlineData("", false)]
    [InlineData("1,2", false)] // picking extra options is not "the one correct answer"
    public void Single_choice_needs_exactly_the_right_option(string given, bool expected) =>
        AnswerGrader.GradeChoice(Choice(QuestionType.Single, 1), given).Correct.ShouldBe(expected);

    [Theory]
    [InlineData("0,2", true)]
    [InlineData("2,0", true)]  // order doesn't matter
    [InlineData("0", false)]   // partial selections score nothing
    [InlineData("0,1,2", false)]
    public void Multi_choice_needs_every_right_option_and_nothing_else(string given, bool expected) =>
        AnswerGrader.GradeChoice(Choice(QuestionType.Multi, 0, 2), given).Correct.ShouldBe(expected);

    [Fact]
    public void A_correct_choice_answer_earns_the_question_s_points()
    {
        var q = Choice(QuestionType.Single, 0);
        AnswerGrader.GradeChoice(q, "0").Points.ShouldBe(q.MaxPoints);
        AnswerGrader.GradeChoice(q, "3").Points.ShouldBe(0m);
    }

    [Fact]
    public void Written_answers_are_marked_on_meaning_not_wording()
    {
        var q = Written("Žalba se izjavljuje u roku od 15 dana od dana prijema rješenja.",
            "žalba", "15 dana", "prijem rješenja");

        // Same substance, different words and without diacritics — still full marks.
        var good = AnswerGrader.GradeByKeywords(q, "zalba se podnosi u roku 15 dana od prijema rjesenja", "bs");
        good.Correct.ShouldBeTrue();
        good.Points.ShouldBe(q.MaxPoints);
    }

    [Fact]
    public void A_partly_right_written_answer_earns_partial_credit()
    {
        var q = Written("Rok za žalbu je 15 dana od prijema rješenja, a odlaže izvršenje.",
            "žalba", "15 dana", "prijem rješenja", "odlaže izvršenje");

        var partial = AnswerGrader.GradeByKeywords(q, "žalba se podnosi u roku od 15 dana, ne sjećam se ostalog", "bs");
        partial.Correct.ShouldBeFalse();
        partial.Points.ShouldBeGreaterThan(0m);
        partial.Points.ShouldBeLessThan(q.MaxPoints);
    }

    [Fact]
    public void An_off_topic_written_answer_scores_nothing()
    {
        var q = Written("Rok za žalbu je 15 dana.", "žalba", "15 dana", "rješenje");
        AnswerGrader.GradeByKeywords(q, "ne znam ovo pitanje", "bs").Points.ShouldBe(0m);
    }

    [Fact]
    public void A_one_word_answer_cannot_reach_full_marks()
    {
        var q = Written("Rok za žalbu je 15 dana.", "žalba");
        AnswerGrader.GradeByKeywords(q, "žalba", "bs").Correct.ShouldBeFalse();
    }

    [Theory]
    [InlineData(100, 5)]
    [InlineData(90, 5)]
    [InlineData(89, 4)]
    [InlineData(80, 4)]
    [InlineData(70, 3)]
    [InlineData(60, 2)]
    [InlineData(59, 1)]
    [InlineData(0, 1)]
    public void Percentages_map_to_the_local_one_to_five_scale(int percent, int grade) =>
        GradeScale.Grade(percent).ShouldBe(grade);

    [Theory]
    [InlineData(60, true)]
    [InlineData(59, false)]
    public void Sixty_percent_is_the_pass_mark(int percent, bool passed) =>
        GradeScale.Passed(percent).ShouldBe(passed);

    [Fact]
    public void Percent_rounds_from_points_and_survives_an_empty_paper()
    {
        GradeScale.Percent(15m, 20m).ShouldBe(75);
        GradeScale.Percent(1m, 3m).ShouldBe(33);
        GradeScale.Percent(0m, 0m).ShouldBe(0);
    }

    [Fact]
    public void Picked_options_parse_from_the_wire_format()
    {
        AnswerGrader.ParseIndices("0, 2 ,3").ShouldBe(new HashSet<int> { 0, 2, 3 });
        AnswerGrader.ParseIndices("").ShouldBeEmpty();
        AnswerGrader.ParseIndices("x,-1").ShouldBeEmpty();
    }
}

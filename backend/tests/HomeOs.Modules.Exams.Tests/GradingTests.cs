using HomeOs.Modules.Exams.Bank;
using HomeOs.Modules.Exams.Grading;
using HomeOs.Platform.Assistant;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace HomeOs.Modules.Exams.Tests;

/// <summary>Marking rules: choice questions locally by set equality, written answers by an AI examiner, then the 1–5 scale.</summary>
public class GradingTests
{
    private static BankQuestion Choice(QuestionType type, params int[] correct) => new()
    {
        Id = "t-1", Law = "zup", Type = type, Text = "?",
        Options = ["a", "b", "c", "d"], Correct = correct,
    };

    private static BankQuestion Written(string id = "t-2") => new()
    {
        Id = id, Law = "zup", Type = QuestionType.Open, Text = "?",
        Answer = "Žalba se izjavljuje u roku od 15 dana od dana prijema rješenja.",
        Keywords = ["žalba", "15 dana"],
    };

    private static AnswerGrader Grader(IAssistant assistant) =>
        new(assistant, NullLogger<AnswerGrader>.Instance);

    // ---- Multiple choice: decided locally, no examiner involved ----

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
    public void A_correct_choice_answer_earns_the_question_s_points_and_is_always_graded()
    {
        var q = Choice(QuestionType.Single, 0);
        var right = AnswerGrader.GradeChoice(q, "0");
        right.Points.ShouldBe(q.MaxPoints);
        right.Graded.ShouldBeTrue();
        right.AiGraded.ShouldBeFalse();
        AnswerGrader.GradeChoice(q, "3").Points.ShouldBe(0m);
    }

    // ---- Written answers ----

    [Fact]
    public async Task Without_an_examiner_written_answers_are_left_out_of_the_score()
    {
        var grader = Grader(new FakeAssistant { Configured = false });

        var verdicts = await grader.GradeOpenAsync([new OpenSubmission(Written(), "neki odgovor")], "bs");

        var verdict = verdicts["t-2"];
        verdict.Graded.ShouldBeFalse();
        verdict.Points.ShouldBe(0m);
        verdict.Correct.ShouldBeFalse();
        verdict.Feedback.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Without_an_examiner_even_a_blank_written_answer_is_left_out()
    {
        var grader = Grader(new FakeAssistant { Configured = false });

        var verdicts = await grader.GradeOpenAsync([new OpenSubmission(Written(), "")], "bs");

        verdicts["t-2"].Graded.ShouldBeFalse();
    }

    [Fact]
    public async Task With_an_examiner_a_blank_answer_scores_zero_but_still_counts()
    {
        var grader = Grader(new FakeAssistant { Configured = true, Reply = "[]" });

        var verdicts = await grader.GradeOpenAsync([new OpenSubmission(Written(), "   ")], "bs");

        var verdict = verdicts["t-2"];
        verdict.Graded.ShouldBeTrue();
        verdict.Points.ShouldBe(0m);
    }

    [Fact]
    public async Task The_examiner_s_points_and_feedback_are_applied()
    {
        var grader = Grader(new FakeAssistant
        {
            Configured = true,
            Reply = """[{"id":"t-2","points":2,"feedback":"Pokriva sve elemente."}]""",
        });

        var verdicts = await grader.GradeOpenAsync([new OpenSubmission(Written(), "žalba u roku od 15 dana")], "bs");

        var verdict = verdicts["t-2"];
        verdict.Graded.ShouldBeTrue();
        verdict.AiGraded.ShouldBeTrue();
        verdict.Correct.ShouldBeTrue();
        verdict.Points.ShouldBe(2m);
        verdict.Feedback.ShouldBe("Pokriva sve elemente.");
    }

    [Fact]
    public async Task The_examiner_cannot_award_more_than_the_question_is_worth()
    {
        var grader = Grader(new FakeAssistant { Configured = true, Reply = """[{"id":"t-2","points":9}]""" });

        var verdicts = await grader.GradeOpenAsync([new OpenSubmission(Written(), "odgovor")], "bs");

        verdicts["t-2"].Points.ShouldBe(Written().MaxPoints);
    }

    [Fact]
    public async Task An_unusable_examiner_reply_leaves_the_question_ungraded_rather_than_guessed()
    {
        var grader = Grader(new FakeAssistant { Configured = true, Reply = "sorry, I can't do that" });

        var verdicts = await grader.GradeOpenAsync([new OpenSubmission(Written(), "odgovor")], "bs");

        verdicts["t-2"].Graded.ShouldBeFalse();
    }

    [Fact]
    public async Task Questions_the_examiner_skipped_are_left_ungraded()
    {
        // The reply marks only the first of two questions.
        var grader = Grader(new FakeAssistant { Configured = true, Reply = """[{"id":"a","points":2}]""" });

        var verdicts = await grader.GradeOpenAsync(
            [new OpenSubmission(Written("a"), "x"), new OpenSubmission(Written("b"), "y")], "bs");

        verdicts["a"].Graded.ShouldBeTrue();
        verdicts["b"].Graded.ShouldBeFalse();
    }

    // ---- The scale ----

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
    public void A_paper_where_nothing_could_be_marked_is_not_graded_rather_than_failed()
    {
        GradeScale.From(0m, 0m).ShouldBe((0, 0, false));
        GradeScale.From(9m, 12m).ShouldBe((75, 3, true));
    }

    [Fact]
    public void Picked_options_parse_from_the_wire_format()
    {
        AnswerGrader.ParseIndices("0, 2 ,3").ShouldBe(new HashSet<int> { 0, 2, 3 });
        AnswerGrader.ParseIndices("").ShouldBeEmpty();
        AnswerGrader.ParseIndices("x,-1").ShouldBeEmpty();
    }

    /// <summary>Stand-in for the platform assistant: answers with a canned reply, or reports itself unconfigured.</summary>
    private sealed class FakeAssistant : IAssistant
    {
        public bool Configured { get; init; }
        public string Reply { get; init; } = string.Empty;

        public Task<AssistantReply> ChatAsync(IReadOnlyList<ChatTurn> history, CancellationToken ct = default) =>
            Task.FromResult(new AssistantReply(Configured, Reply, []));

        public Task<string> SummarizeAsync(string language, string content, CancellationToken ct = default) =>
            Task.FromResult(Reply);

        public Task<string> CompleteAsync(string system, string user, CancellationToken ct = default) =>
            Task.FromResult(Configured ? Reply : string.Empty);
    }
}

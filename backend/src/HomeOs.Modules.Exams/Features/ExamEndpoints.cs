using System.Globalization;
using HomeOs.Modules.Exams.Bank;
using HomeOs.Modules.Exams.Domain;
using HomeOs.Modules.Exams.Grading;
using HomeOs.Modules.Exams.Persistence;
using HomeOs.Platform.Events;
using HomeOs.Platform.Members;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HomeOs.Modules.Exams.Features;

/// <summary>
/// Exam practice for the professional exam: draw a paper from the question bank, answer it, and get it marked.
/// Attempts are <em>personal</em> — a member only ever sees their own, managers included, because a study
/// record is not household business.
/// </summary>
public static class ExamEndpoints
{
    private const int MinQuestions = 5;
    private const int MaxQuestions = 100;

    public static IEndpointRouteBuilder MapExamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/exams").RequireAuthorization().WithTags("Exams");

        group.MapGet("/subjects", (QuestionBank bank, AnswerGrader grader) =>
                Results.Ok(new SubjectsDto(
                    [.. bank.Laws().Select(l => new LawDto(l.Code, l.Title, l.ShortTitle, l.Gazette, l.Total, l.Choice, l.Open))],
                    bank.All.Count, grader.AiAvailable, GradeScale.PassPercent)))
            .WithName("ListExamSubjects");

        // `law` takes one code or a comma-separated list ("zup,znr") so revision can mix laws the same
        // way an exam paper does; omitting it studies the whole bank.
        group.MapGet("/study", (string? law, string? q, int? skip, int? take, QuestionBank bank) =>
            {
                var laws = ParseLaws(law);
                var all = bank.Study(laws, q);
                var page = all.Skip(Math.Max(skip ?? 0, 0)).Take(Math.Clamp(take ?? 50, 1, 200)).ToList();
                return Results.Ok(new StudyPageDto(all.Count, [.. page.Select(ToStudyDto)]));
            })
            .WithName("StudyExamQuestions");

        group.MapGet("/attempts", async (ICurrentMember me, ExamsDbContext db, CancellationToken ct) =>
            {
                var attempts = await db.Attempts.AsNoTracking()
                    .Where(a => a.HouseholdId == me.HouseholdId && a.MemberId == me.Id)
                    .OrderByDescending(a => a.StartedAtUtc)
                    .Take(50)
                    .Select(a => new AttemptSummaryDto(a.Id, a.Laws, a.Mode, a.StartedAtUtc, a.FinishedAtUtc,
                        a.Answers.Count, a.EarnedPoints, a.MaxPoints, a.Percent, a.Grade, a.Passed))
                    .ToListAsync(ct);
                return Results.Ok(attempts);
            })
            .WithName("ListExamAttempts");

        group.MapPost("/attempts", async (StartExamRequest req, ICurrentMember me, ExamsDbContext db,
            QuestionBank bank, CancellationToken ct) =>
            {
                var mode = Normalize(req.Mode);
                var laws = (req.Laws ?? []).Where(l => LawCatalog.All.ContainsKey(l)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var count = Math.Clamp(req.Count ?? 20, MinQuestions, MaxQuestions);

                var drawn = bank.Draw(laws, count, mode, Random.Shared);
                if (drawn.Count == 0) return Results.BadRequest(new { code = "exams.noQuestions" });

                var attempt = ExamAttempt.Start(me.HouseholdId, me.Id, string.Join(',', laws), mode);
                for (var i = 0; i < drawn.Count; i++)
                    attempt.Answers.Add(ExamAnswer.Place(attempt.Id, drawn[i].Id, i, drawn[i].MaxPoints));

                db.Attempts.Add(attempt);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/exams/attempts/{attempt.Id}", ToAttemptDto(attempt, bank));
            })
            .WithName("StartExamAttempt");

        group.MapGet("/attempts/{id:guid}", async (Guid id, ICurrentMember me, ExamsDbContext db,
            QuestionBank bank, CancellationToken ct) =>
            {
                var attempt = await LoadAsync(db, me, id, tracking: false, ct);
                return attempt is null ? Results.NotFound() : Results.Ok(ToAttemptDto(attempt, bank));
            })
            .WithName("GetExamAttempt");

        group.MapPut("/attempts/{id:guid}/answers/{questionId}", async (Guid id, string questionId,
            SaveAnswerRequest req, ICurrentMember me, ExamsDbContext db, CancellationToken ct) =>
            {
                var attempt = await LoadAsync(db, me, id, tracking: true, ct);
                if (attempt is null) return Results.NotFound();
                // A finished paper is a record; it must not change after it was marked.
                if (attempt.IsFinished) return Results.Conflict(new { code = "exams.alreadyFinished" });

                var answer = attempt.Answers.FirstOrDefault(a => a.QuestionId == questionId);
                if (answer is null) return Results.NotFound();

                answer.Answer(req.Answer ?? string.Empty);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithName("SaveExamAnswer");

        group.MapPost("/attempts/{id:guid}/finish", async (Guid id, ICurrentMember me, ExamsDbContext db,
            QuestionBank bank, AnswerGrader grader, IEventBus bus, CancellationToken ct) =>
            {
                var attempt = await LoadAsync(db, me, id, tracking: true, ct);
                if (attempt is null) return Results.NotFound();
                if (attempt.IsFinished) return Results.Ok(ToAttemptDto(attempt, bank));

                var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                var open = new List<OpenSubmission>();

                foreach (var answer in attempt.Answers)
                {
                    var question = bank.Find(answer.QuestionId);
                    if (question is null) continue;
                    if (question.Type == QuestionType.Open)
                    {
                        open.Add(new OpenSubmission(question, answer.Given));
                        continue;
                    }
                    var verdict = AnswerGrader.GradeChoice(question, answer.Given);
                    answer.Score(verdict.Points, verdict.Correct, verdict.Feedback, false);
                }

                var openVerdicts = await grader.GradeOpenAsync(open, language, ct);
                foreach (var answer in attempt.Answers)
                {
                    if (!openVerdicts.TryGetValue(answer.QuestionId, out var verdict)) continue;
                    answer.Score(verdict.Points, verdict.Correct, verdict.Feedback, verdict.AiGraded);
                }

                var earned = attempt.Answers.Sum(a => a.Points);
                var max = attempt.Answers.Sum(a => a.MaxPoints);
                var percent = GradeScale.Percent(earned, max);
                attempt.Finish(earned, max, percent, GradeScale.Grade(percent), GradeScale.Passed(percent));
                await db.SaveChangesAsync(ct);

                // Announce it so automations ("when I finish an exam, notify me") and the audit stream can react.
                await bus.PublishAsync(new AppActivity(me.HouseholdId, me.Id, "exam.finished",
                    $"{percent}% ({attempt.Grade})", "/exams"), ct);

                return Results.Ok(ToAttemptDto(attempt, bank));
            })
            .WithName("FinishExamAttempt");

        group.MapDelete("/attempts/{id:guid}", async (Guid id, ICurrentMember me, ExamsDbContext db, CancellationToken ct) =>
            {
                var attempt = await LoadAsync(db, me, id, tracking: true, ct);
                if (attempt is null) return Results.NotFound();
                db.Attempts.Remove(attempt);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithName("DeleteExamAttempt");

        return app;
    }

    // ---- Helpers ----

    private static string Normalize(string? mode) =>
        mode?.ToLowerInvariant() is "choice" or "open" ? mode.ToLowerInvariant() : "mixed";

    /// <summary>Reads a single law code or a comma-separated list, dropping anything not in the catalogue.</summary>
    private static List<string> ParseLaws(string? law) =>
    [
        .. (law ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(LawCatalog.All.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>Loads an attempt belonging to the caller — the only place attempt ownership is decided.</summary>
    private static Task<ExamAttempt?> LoadAsync(ExamsDbContext db, ICurrentMember me, Guid id, bool tracking, CancellationToken ct)
    {
        var query = tracking ? db.Attempts : db.Attempts.AsNoTracking();
        return query.Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == id && a.HouseholdId == me.HouseholdId && a.MemberId == me.Id, ct);
    }

    private static ExamAttemptDto ToAttemptDto(ExamAttempt attempt, QuestionBank bank)
    {
        var finished = attempt.IsFinished;
        var questions = attempt.Answers
            .OrderBy(a => a.Ordinal)
            .Select(a =>
            {
                var q = bank.Find(a.QuestionId);
                var law = q is not null && LawCatalog.All.TryGetValue(q.Law, out var meta) ? meta.Short : q?.Law ?? "";
                return new ExamQuestionDto(
                    a.QuestionId, a.Ordinal, q?.Law ?? "", law, q?.Article, q?.Topic,
                    (q?.Type ?? QuestionType.Single).ToString().ToLowerInvariant(),
                    q?.Text ?? "", q?.Options ?? [], a.MaxPoints, a.Given,
                    // Everything below is the mark sheet — only ever sent once the paper is closed.
                    finished ? a.Points : null,
                    finished ? a.Correct : null,
                    finished ? a.Feedback : null,
                    finished && a.AiGraded,
                    finished ? q?.Correct ?? [] : [],
                    finished ? q?.Answer : null,
                    finished ? q?.Explanation : null);
            })
            .ToList();

        return new ExamAttemptDto(attempt.Id, attempt.Laws, attempt.Mode, attempt.StartedAtUtc, attempt.FinishedAtUtc,
            finished, attempt.EarnedPoints, attempt.MaxPoints, attempt.Percent, attempt.Grade, attempt.Passed, questions);
    }

    private static StudyQuestionDto ToStudyDto(BankQuestion q)
    {
        var law = LawCatalog.All.TryGetValue(q.Law, out var meta) ? meta.Short : q.Law;
        return new StudyQuestionDto(q.Id, q.Law, law, q.Article, q.Topic, q.Type.ToString().ToLowerInvariant(),
            q.Text, q.Options, q.Correct, q.Answer, q.Explanation);
    }
}

/// <summary>The laws on offer plus how the exam will be marked.</summary>
public sealed record SubjectsDto(IReadOnlyList<LawDto> Laws, int TotalQuestions, bool AiGrading, int PassPercent);

/// <summary>One law in the bank with its question counts.</summary>
public sealed record LawDto(string Code, string Title, string ShortTitle, string Gazette, int Total, int Choice, int Open);

/// <summary>Start a new paper.</summary>
public sealed record StartExamRequest(IReadOnlyList<string>? Laws, int? Count, string? Mode);

/// <summary>Save what the candidate wrote/picked for one question.</summary>
public sealed record SaveAnswerRequest(string? Answer);

/// <summary>An attempt with its paper; mark-sheet fields fill in once it is finished.</summary>
public sealed record ExamAttemptDto(
    Guid Id, string Laws, string Mode, DateTimeOffset StartedAtUtc, DateTimeOffset? FinishedAtUtc, bool Finished,
    decimal EarnedPoints, decimal MaxPoints, int Percent, int Grade, bool Passed,
    IReadOnlyList<ExamQuestionDto> Questions);

/// <summary>One question on the paper. Correct answers stay <c>null</c>/empty until the attempt is marked.</summary>
public sealed record ExamQuestionDto(
    string Id, int Ordinal, string Law, string LawShort, string? Article, string? Topic, string Type,
    string Text, IReadOnlyList<string> Options, decimal MaxPoints, string Given,
    decimal? Points, bool? Correct, string? Feedback, bool AiGraded,
    IReadOnlyList<int> CorrectOptions, string? ModelAnswer, string? Explanation);

/// <summary>A past attempt as it appears in the history list.</summary>
public sealed record AttemptSummaryDto(
    Guid Id, string Laws, string Mode, DateTimeOffset StartedAtUtc, DateTimeOffset? FinishedAtUtc,
    int QuestionCount, decimal EarnedPoints, decimal MaxPoints, int Percent, int Grade, bool Passed);

/// <summary>A page of study-mode questions.</summary>
public sealed record StudyPageDto(int Total, IReadOnlyList<StudyQuestionDto> Questions);

/// <summary>A question with its answer — study mode shows everything.</summary>
public sealed record StudyQuestionDto(
    string Id, string Law, string LawShort, string? Article, string? Topic, string Type,
    string Text, IReadOnlyList<string> Options, IReadOnlyList<int> Correct, string? Answer, string? Explanation);

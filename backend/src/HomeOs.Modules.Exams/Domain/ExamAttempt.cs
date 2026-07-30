namespace HomeOs.Modules.Exams.Domain;

/// <summary>One sitting of a practice exam by one member: a fixed set of questions and, once finished, a mark.</summary>
public sealed class ExamAttempt
{
    private ExamAttempt() { }

    /// <summary>Opens an attempt over <paramref name="laws"/> (comma-separated law codes).</summary>
    public static ExamAttempt Start(Guid householdId, Guid memberId, string laws, string mode) => new()
    {
        HouseholdId = householdId,
        MemberId = memberId,
        Laws = laws,
        Mode = mode,
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid HouseholdId { get; private set; }

    /// <summary>Whose attempt this is — results are personal, never shared with the household.</summary>
    public Guid MemberId { get; private set; }

    /// <summary>Comma-separated law codes the questions were drawn from (e.g. <c>zup,znr</c>).</summary>
    public string Laws { get; private set; } = string.Empty;

    /// <summary>Question mix requested: <c>mixed</c>, <c>choice</c> or <c>open</c>.</summary>
    public string Mode { get; private set; } = "mixed";

    public DateTimeOffset StartedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAtUtc { get; private set; }

    /// <summary>Points scored once graded.</summary>
    public decimal EarnedPoints { get; private set; }

    /// <summary>Points available across all questions.</summary>
    public decimal MaxPoints { get; private set; }

    /// <summary>Score as a whole percentage (0–100).</summary>
    public int Percent { get; private set; }

    /// <summary>Mark on the local 1–5 scale (1 = nedovoljan … 5 = odličan).</summary>
    public int Grade { get; private set; }

    /// <summary>Whether the attempt cleared the pass threshold.</summary>
    public bool Passed { get; private set; }

    public List<ExamAnswer> Answers { get; private set; } = [];

    /// <summary>Whether the attempt has been graded and closed.</summary>
    public bool IsFinished => FinishedAtUtc is not null;

    /// <summary>Closes the attempt with the totals computed by the grader.</summary>
    public void Finish(decimal earned, decimal max, int percent, int grade, bool passed)
    {
        FinishedAtUtc = DateTimeOffset.UtcNow;
        EarnedPoints = earned;
        MaxPoints = max;
        Percent = percent;
        Grade = grade;
        Passed = passed;
    }
}

/// <summary>One question inside an attempt, together with what the member answered and how it scored.</summary>
public sealed class ExamAnswer
{
    private ExamAnswer() { }

    /// <summary>Places a question on the paper (unanswered until the member responds).</summary>
    public static ExamAnswer Place(Guid attemptId, string questionId, int ordinal, decimal maxPoints) => new()
    {
        AttemptId = attemptId,
        QuestionId = questionId,
        Ordinal = ordinal,
        MaxPoints = maxPoints,
    };

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid AttemptId { get; private set; }

    /// <summary>Stable id of the bank question (e.g. <c>zup-014</c>).</summary>
    public string QuestionId { get; private set; } = string.Empty;

    /// <summary>Position on the paper, so the order stays stable across reloads.</summary>
    public int Ordinal { get; private set; }

    /// <summary>The member's answer: option indices as <c>"0,2"</c> for choice questions, free text for open ones.</summary>
    public string Given { get; private set; } = string.Empty;

    public decimal Points { get; private set; }
    public decimal MaxPoints { get; private set; }
    public bool Correct { get; private set; }

    /// <summary>Short explanation of the mark — for open questions this is the examiner's (AI) feedback.</summary>
    public string? Feedback { get; private set; }

    /// <summary>True when an AI examiner graded this answer (false = decided locally by comparing options).</summary>
    public bool AiGraded { get; private set; }

    /// <summary>
    /// False when the answer could not be marked at all (a written question with no AI examiner available).
    /// Ungraded answers are excluded from the attempt's points, so a missing key never costs the candidate marks.
    /// </summary>
    public bool Graded { get; private set; } = true;

    /// <summary>Records (or replaces) the member's answer while the attempt is still open.</summary>
    public void Answer(string given) => Given = given.Trim();

    /// <summary>Applies the grader's verdict.</summary>
    public void Score(decimal points, bool correct, string? feedback, bool aiGraded, bool graded = true)
    {
        Points = points;
        Correct = correct;
        Feedback = feedback;
        AiGraded = aiGraded;
        Graded = graded;
    }
}

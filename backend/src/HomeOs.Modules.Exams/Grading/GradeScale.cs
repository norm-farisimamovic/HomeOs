namespace HomeOs.Modules.Exams.Grading;

/// <summary>
/// The local 1–5 marking scale used by schools and professional exams in BiH, plus the pass threshold.
/// Kept in one place so the exam screen, the history list and any future report all mark the same way.
/// </summary>
public static class GradeScale
{
    /// <summary>Percentage from which an attempt counts as passed (a professional exam is typically 60%).</summary>
    public const int PassPercent = 60;

    /// <summary>Turns a percentage into a mark: 1 (nedovoljan) … 5 (odličan).</summary>
    public static int Grade(int percent) => percent switch
    {
        >= 90 => 5,
        >= 80 => 4,
        >= 70 => 3,
        >= 60 => 2,
        _ => 1,
    };

    /// <summary>Whether the percentage clears <see cref="PassPercent"/>.</summary>
    public static bool Passed(int percent) => percent >= PassPercent;

    /// <summary>Rounds points to a whole percentage, guarding against an empty paper.</summary>
    public static int Percent(decimal earned, decimal max) =>
        max <= 0 ? 0 : (int)Math.Round(earned / max * 100m, MidpointRounding.AwayFromZero);
}

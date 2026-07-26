namespace HomeOs.Platform.Scheduling;

/// <summary>
/// Turns a single due date into a ladder of lead-up alerts (e.g. "in 7 days", "in 3 days", "tomorrow",
/// "today") that each fire once. A caller stores the last stage it alerted at and asks
/// <see cref="StageToFire"/> each tick; the stage is the number of days before the date, so <c>0</c> means
/// "due today". Shared by the bill and reminder dispatchers so escalation behaves identically everywhere.
/// </summary>
public static class LeadSchedule
{
    /// <summary>Default ladder for bills — warn a week out, then tighten.</summary>
    public static readonly IReadOnlyList<int> Bills = [7, 3, 1, 0];

    /// <summary>Default ladder for reminders — a shorter run-up suits personal reminders.</summary>
    public static readonly IReadOnlyList<int> Reminders = [3, 1, 0];

    /// <summary>
    /// The stage to alert at now, or <c>null</c> if nothing new is due. <paramref name="daysUntil"/> is
    /// (dueDate − today); negative means overdue. Returns the tightest ladder stage the date has reached,
    /// but only when it's deeper than <paramref name="lastNotifiedStage"/> — so each threshold fires once
    /// and an overdue item never spams after its "today" alert.
    /// </summary>
    public static int? StageToFire(int daysUntil, IReadOnlyList<int> ladder, int? lastNotifiedStage)
    {
        int? tightest = null;
        foreach (var stage in ladder)
            if (daysUntil <= stage)
                tightest = tightest is null ? stage : Math.Min(tightest.Value, stage);

        if (tightest is null) return null;                          // still outside the widest window
        if (lastNotifiedStage is null || tightest < lastNotifiedStage) return tightest;
        return null;                                                // already alerted at this depth (or deeper)
    }
}

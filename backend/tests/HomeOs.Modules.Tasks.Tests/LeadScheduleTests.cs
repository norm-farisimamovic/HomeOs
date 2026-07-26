using HomeOs.Platform.Scheduling;
using Shouldly;

namespace HomeOs.Modules.Tasks.Tests;

public class LeadScheduleTests
{
    private static readonly IReadOnlyList<int> Ladder = [7, 3, 1, 0];

    [Theory]
    [InlineData(10, null, null)] // outside the widest window → nothing
    [InlineData(7, null, 7)]     // hits the 7-day window
    [InlineData(5, null, 7)]     // still in the 7-day bucket
    [InlineData(3, null, 3)]
    [InlineData(1, null, 1)]
    [InlineData(0, null, 0)]     // due today
    [InlineData(-2, null, 0)]    // overdue collapses to the "today" stage
    public void StageToFire_picks_the_tightest_reached_stage(int daysUntil, int? last, int? expected) =>
        LeadSchedule.StageToFire(daysUntil, Ladder, last).ShouldBe(expected);

    [Fact]
    public void Each_threshold_fires_once_as_the_date_approaches()
    {
        int? last = null;

        // 5 days out → 7-day stage fires.
        var s1 = LeadSchedule.StageToFire(5, Ladder, last);
        s1.ShouldBe(7);
        last = s1;

        // Still 5 days out on the next tick → no repeat.
        LeadSchedule.StageToFire(5, Ladder, last).ShouldBeNull();

        // 2 days out → 3-day stage fires.
        var s2 = LeadSchedule.StageToFire(2, Ladder, last);
        s2.ShouldBe(3);
        last = s2;

        // Due today → 0 stage fires.
        var s3 = LeadSchedule.StageToFire(0, Ladder, last);
        s3.ShouldBe(0);
        last = s3;

        // Overdue → already alerted at the deepest stage, so silent.
        LeadSchedule.StageToFire(-1, Ladder, last).ShouldBeNull();
    }
}

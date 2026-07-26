using HomeOs.Modules.Finance.Domain;
using HomeOs.Modules.Reminders.Domain;
using HomeOs.Modules.Tasks.Domain;
using HomeOs.Platform.Entities;
using Shouldly;

namespace HomeOs.Architecture.Tests;

/// <summary>Cross-module domain rules for recurrence/roll-forward (pure, no database).</summary>
public class RecurrenceTests
{
    private static readonly Guid H = Guid.NewGuid();
    private static readonly Guid M = Guid.NewGuid();

    // ---- Bills ----

    [Fact]
    public void Recurring_bill_rolls_to_the_next_future_occurrence_and_resets_its_ladder()
    {
        var today = new DateOnly(2026, 7, 24);
        var bill = Bill.Create(H, M, "Internet", 30m, "BAM", BillCadence.Monthly,
            new DateOnly(2026, 5, 23), "Utilities", M, Visibility.Household);
        bill.MarkNotified(0);

        bill.RollForwardTo(today);

        bill.NextDue.ShouldBe(new DateOnly(2026, 8, 23)); // skips missed May/June/July cycles → first future
        bill.NotifiedLeadDays.ShouldBeNull();             // fresh ladder for the new cycle
    }

    [Fact]
    public void One_off_bill_never_rolls_forward()
    {
        var due = new DateOnly(2026, 1, 1);
        var bill = Bill.Create(H, M, "Deposit", 100m, "BAM", BillCadence.OneOff, due, "Other", M, Visibility.Household);

        bill.RollForwardTo(new DateOnly(2026, 7, 24));

        bill.NextDue.ShouldBe(due);
        bill.IsRecurring.ShouldBeFalse();
    }

    // ---- Reminders ----

    [Fact]
    public void Recurring_reminder_advances_past_today_and_stays_active_on_complete()
    {
        var today = new DateOnly(2026, 7, 24);
        var reminder = Reminder.Create(H, M, M, "Water plants",
            new DateOnly(2026, 7, 20), null, null, Visibility.Private, Recurrence.Weekly);

        reminder.Complete(today);

        reminder.RemindOn.ShouldBe(new DateOnly(2026, 7, 27)); // next weekly slot after today
        reminder.IsDone.ShouldBeFalse();
    }

    [Fact]
    public void One_off_reminder_is_marked_done_on_complete()
    {
        var reminder = Reminder.Create(H, M, M, "Call bank",
            new DateOnly(2026, 7, 24), null, null, Visibility.Private);

        reminder.Complete(new DateOnly(2026, 7, 24));

        reminder.IsDone.ShouldBeTrue();
    }

    // ---- Tasks ----

    [Fact]
    public void Recurring_task_rolls_its_due_date_and_reopens_on_complete()
    {
        var today = new DateOnly(2026, 7, 24);
        var task = TaskItem.Create(H, M, "Take out bins", null, today, M,
            TaskPriority.Normal, Visibility.Household, null, TaskRecurrence.Weekly);

        task.Complete(today);

        task.DueDate.ShouldBe(new DateOnly(2026, 7, 31));
        task.IsDone.ShouldBeFalse();
        task.Status.ShouldBe(TaskItemStatus.Todo);
    }

    [Fact]
    public void One_off_task_completes_normally()
    {
        var task = TaskItem.Create(H, M, "File taxes", null, new DateOnly(2026, 7, 24), M,
            TaskPriority.High, Visibility.Household, null);

        task.Complete(new DateOnly(2026, 7, 24));

        task.IsDone.ShouldBeTrue();
        task.Status.ShouldBe(TaskItemStatus.Done);
    }
}

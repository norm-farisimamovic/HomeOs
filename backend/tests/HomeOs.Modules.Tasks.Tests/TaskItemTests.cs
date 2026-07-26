using HomeOs.Modules.Tasks.Domain;
using HomeOs.Platform.Entities;
using Shouldly;

namespace HomeOs.Modules.Tasks.Tests;

public class TaskItemTests
{
    private static readonly Guid Household = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    private static TaskItem NewTask(DateOnly? due = null, IEnumerable<string>? tags = null) =>
        TaskItem.Create(Household, Owner, "  Book the boiler service  ", null, due, null,
            TaskPriority.Normal, Visibility.Household, tags);

    [Fact]
    public void Create_trims_title_and_sets_ownership()
    {
        var task = NewTask();

        task.Title.ShouldBe("Book the boiler service");
        task.HouseholdId.ShouldBe(Household);
        task.OwnerId.ShouldBe(Owner);
        task.ObjectType.ShouldBe("task");
        task.Status.ShouldBe(TaskItemStatus.Todo);
        task.IsDone.ShouldBeFalse();
    }

    [Fact]
    public void Create_deduplicates_and_trims_tags_and_drops_blanks()
    {
        var task = NewTask(tags: ["home", " home ", "bills", "  "]);

        task.Tags.ShouldBe(["home", "bills"]);
    }

    [Fact]
    public void Complete_then_Reopen_toggles_status_and_timestamp()
    {
        var task = NewTask();

        task.Complete();
        task.IsDone.ShouldBeTrue();
        task.Status.ShouldBe(TaskItemStatus.Done);
        task.CompletedAtUtc.ShouldNotBeNull();

        task.Reopen();
        task.IsDone.ShouldBeFalse();
        task.Status.ShouldBe(TaskItemStatus.Todo);
        task.CompletedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void IsOverdue_is_true_only_for_past_due_and_not_done()
    {
        var today = new DateOnly(2026, 7, 23);

        NewTask(due: today.AddDays(-1)).IsOverdue(today).ShouldBeTrue();
        NewTask(due: today).IsOverdue(today).ShouldBeFalse();
        NewTask(due: today.AddDays(1)).IsOverdue(today).ShouldBeFalse();
        NewTask(due: null).IsOverdue(today).ShouldBeFalse();

        var doneButPast = NewTask(due: today.AddDays(-2));
        doneButPast.Complete();
        doneButPast.IsOverdue(today).ShouldBeFalse();
    }

    [Fact]
    public void Update_replaces_fields_and_normalizes_tags()
    {
        var task = NewTask();
        var assignee = Guid.NewGuid();

        task.Update("New title", "details", new DateOnly(2026, 8, 1), assignee,
            TaskPriority.High, Visibility.Private, ["a", "a", "b"]);

        task.Title.ShouldBe("New title");
        task.Description.ShouldBe("details");
        task.AssigneeId.ShouldBe(assignee);
        task.Priority.ShouldBe(TaskPriority.High);
        task.Visibility.ShouldBe(Visibility.Private);
        task.Tags.ShouldBe(["a", "b"]);
    }
}

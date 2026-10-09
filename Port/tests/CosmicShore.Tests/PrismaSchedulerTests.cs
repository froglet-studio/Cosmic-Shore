using System;
using System.Linq;
using Prisma;
using Prisma.Workspace;

namespace CosmicShore.Tests;

/// <summary>
/// The scheduler's logic (Port/src/Shared/Workspace): the quick-add line, the TODAY / UPCOMING /
/// overdue queries, repeat rules and what completing a repeating card does. "Now" is Friday
/// 2026-10-09, 10:00 local, unless a test says otherwise.
/// </summary>
public class PrismaSchedulerTests
{
    static readonly DateTime Now = new(2026, 10, 9, 10, 0, 0);   // a Friday
    static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    // ------------------------------------------------------------------ quick add

    [Fact]
    public void QuickAdd_TheExampleLine()
    {
        var q = QuickAdd.Parse("Fix trail tomorrow 3pm #vfx !1 ~45m", Now);
        Assert.Equal("Fix trail", q.Title);
        Assert.Equal(D(10, 10), q.Scheduled);
        Assert.Equal(new TimeOnly(15, 0), q.ScheduledTime);
        Assert.Equal(new[] { "vfx" }, q.Tags);
        Assert.Equal(1, q.Priority);
        Assert.Equal(45, q.EstimateMinutes);
        Assert.Null(q.Due);
        Assert.Null(q.Recurrence);
        Assert.Equal(PrismaBoard.Kind.Task, q.Type);
    }

    [Theory]
    [InlineData("Call the art team mon", "Call the art team", 10, 12)]
    [InlineData("Review fri", "Review", 10, 9)]                 // a weekday includes today
    [InlineData("Review next fri", "Review", 10, 16)]           // "next" is strictly after today
    [InlineData("Review next mon", "Review", 10, 12)]
    [InlineData("next week planning", "planning", 10, 12)]
    [InlineData("Write report in 3 days", "Write report", 10, 12)]
    [InlineData("Write report in 2 weeks", "Write report", 10, 23)]
    [InlineData("Plan sprint oct 20", "Plan sprint", 10, 20)]
    [InlineData("Plan sprint 20th october", "Plan sprint", 10, 20)]
    [InlineData("Kickoff 2026-11-02", "Kickoff", 11, 2)]
    [InlineData("Playtest on sun", "Playtest", 10, 11)]
    [InlineData("Playtest on sunday", "Playtest", 10, 11)]
    [InlineData("Ship it today", "Ship it", 10, 9)]
    [InlineData("Ship it tmrw", "Ship it", 10, 10)]
    public void QuickAdd_Days(string line, string title, int month, int day)
    {
        var q = QuickAdd.Parse(line, Now);
        Assert.Equal(title, q.Title);
        Assert.Equal(D(month, day), q.Scheduled);
    }

    [Fact]
    public void QuickAdd_APastMonthDay_IsNextYear()
    {
        Assert.Equal(D(1, 5, 2027), QuickAdd.Parse("Plan 5 jan", Now).Scheduled);
    }

    [Theory]
    [InlineData("Polish sun shader")]          // "sun" and "sat" are words unless a date is expected
    [InlineData("Fix sat dish reflections")]
    [InlineData("Plan may")]                   // a month needs a day number
    [InlineData("Fix 3 bugs")]
    [InlineData("Meet at the hangar")]
    [InlineData("Add next button")]
    [InlineData("Weekly report")]              // only "every ..." repeats
    public void QuickAdd_LeavesOrdinaryWordsInTheTitle(string line)
    {
        var q = QuickAdd.Parse(line, Now);
        Assert.Equal(line, q.Title);
        Assert.Null(q.Scheduled);
        Assert.Null(q.ScheduledTime);
        Assert.Null(q.Recurrence);
    }

    [Theory]
    [InlineData("Sync 3:30pm", 15, 30)]
    [InlineData("Sync at 4 pm", 16, 0)]
    [InlineData("Sync 17:45", 17, 45)]
    [InlineData("Sync noon", 12, 0)]
    [InlineData("Sync 12am", 0, 0)]
    public void QuickAdd_Times(string line, int h, int m)
    {
        var q = QuickAdd.Parse(line, Now);
        Assert.Equal("Sync", q.Title);
        Assert.Equal(new TimeOnly(h, m), q.ScheduledTime);
    }

    [Fact]
    public void QuickAdd_ATimeWithoutADay_IsTodayOrTomorrowIfItPassed()
    {
        Assert.Equal(D(10, 9), QuickAdd.Parse("Sync 3pm", Now).Scheduled);
        Assert.Equal(D(10, 10), QuickAdd.Parse("Sync at 9am", Now).Scheduled);
    }

    [Fact]
    public void QuickAdd_DueIsTheDeadline_AndTakesItsOwnTime()
    {
        var q = QuickAdd.Parse("Ship build fri due 2026-10-20 5pm", Now);
        Assert.Equal("Ship build", q.Title);
        Assert.Equal(D(10, 9), q.Scheduled);
        Assert.Null(q.ScheduledTime);
        Assert.Equal(D(10, 20), q.Due);
        Assert.Equal(new TimeOnly(17, 0), q.DueTime);
    }

    [Theory]
    [InlineData("~45m", 45)]
    [InlineData("~45", 45)]
    [InlineData("~2h", 120)]
    [InlineData("~1h30m", 90)]
    [InlineData("~1.5h", 90)]
    [InlineData("~10min", 10)]
    public void QuickAdd_Estimates(string token, int minutes)
    {
        var q = QuickAdd.Parse("Profile Bloomrush " + token, Now);
        Assert.Equal("Profile Bloomrush", q.Title);
        Assert.Equal(minutes, q.EstimateMinutes);
    }

    [Fact]
    public void QuickAdd_KindPriorityAssigneeTagsAndCriterion()
    {
        var q = QuickAdd.Parse("bug: Crash in hangar !high @Yash #vfx #VFX #audio done when: engine_smoke has 0 exceptions", Now);
        Assert.Equal(PrismaBoard.Kind.Bug, q.Type);
        Assert.Equal("Crash in hangar", q.Title);
        Assert.Equal(1, q.Priority);
        Assert.Equal("Yash", q.Assignee);
        Assert.Equal(new[] { "vfx", "audio" }, q.Tags);
        Assert.Equal("engine_smoke has 0 exceptions", q.Criterion);
        Assert.Equal(3, QuickAdd.Parse("Tidy !3", Now).Priority);
    }

    [Theory]
    [InlineData("Standup every weekday 10:30", "Standup", "weekdays", 10, 9)]
    [InlineData("Water plants every mon,thu", "Water plants", "weekly:mon,thu", 10, 12)]
    [InlineData("Water plants every mon and thu", "Water plants", "weekly:mon,thu", 10, 12)]
    [InlineData("Retro every 2 weeks", "Retro", "weeks:2", 10, 9)]
    [InlineData("Backup every day", "Backup", "daily", 10, 9)]
    [InlineData("Invoice every month", "Invoice", "monthly", 10, 9)]
    [InlineData("Playtest every sat", "Playtest", "weekly:sat", 10, 10)]
    public void QuickAdd_Repeats_StartingAtTheFirstOccurrence(string line, string title, string rule, int month, int day)
    {
        var q = QuickAdd.Parse(line, Now);
        Assert.Equal(title, q.Title);
        Assert.Equal(rule, q.Recurrence);
        Assert.Equal(D(month, day), q.Scheduled);
    }

    [Fact]
    public void AddFromQuickAdd_FillsTheCard_AndSkipsAnEmptyLine()
    {
        var b = new PrismaBoard();
        var it = BoardCommands.AddFromQuickAdd(b, "Fix trail tomorrow 3pm #vfx !1 ~45m @yash every week", Now)!;
        Assert.Equal("T-1", it.Id);
        Assert.Equal("Fix trail", it.Title);
        Assert.Equal(PrismaBoard.Status.Todo, it.State);
        Assert.Equal(1, it.Priority);
        Assert.Equal(D(10, 10), it.Scheduled);
        Assert.Equal(new TimeOnly(15, 0), it.ScheduledTime);
        Assert.Equal("yash", it.Assignee);
        Assert.Equal(45, it.EstimateMinutes);
        Assert.Equal("weekly", it.Recurrence);
        Assert.Null(BoardCommands.AddFromQuickAdd(b, "#vfx !1 tomorrow", Now));
        Assert.Single(b.Items);
    }

    // ------------------------------------------------------------------ queries

    static (PrismaBoard board, Func<string, PrismaBoard.Item> card) Fixture()
    {
        var b = new PrismaBoard();
        PrismaBoard.Item Add(string title, int priority = 2, PrismaBoard.Status state = PrismaBoard.Status.Todo,
                              DateOnly? sched = null, TimeOnly? time = null, DateOnly? due = null, TimeOnly? dueTime = null)
        {
            var i = b.Add(PrismaBoard.Kind.Task, title, state: state, priority: priority);
            i.Scheduled = sched; i.ScheduledTime = time; i.Due = due; i.DueTime = dueTime;
            return i;
        }
        Add("A overdue yesterday", due: D(10, 8));
        Add("B today 09:00", sched: D(10, 9), time: new TimeOnly(9, 0));
        Add("C today untimed P1", priority: 1, sched: D(10, 9));
        Add("D carried over", sched: D(10, 7));
        Add("E due today 13:00", due: D(10, 9), dueTime: new TimeOnly(13, 0));
        Add("F tomorrow", sched: D(10, 10));
        Add("G due monday", due: D(10, 12));
        Add("H done today", state: PrismaBoard.Status.Done, sched: D(10, 9));
        Add("I suggested today", state: PrismaBoard.Status.Suggested, sched: D(10, 9));
        Add("J in eleven days", sched: D(10, 20));
        Add("K backlog", priority: 3);
        Add("L tomorrow 08:00", sched: D(10, 10), time: new TimeOnly(8, 0));
        return (b, t => b.Items.Single(i => i.Title.StartsWith(t + " ")));
    }

    [Fact]
    public void Today_OverdueFirst_ThenByTime_ThenPriority_OpenCardsOnly()
    {
        var (b, _) = Fixture();
        var at2pm = new DateTime(2026, 10, 9, 14, 0, 0);
        Assert.Equal(new[] { "E", "A", "B", "C", "D" }, BoardQueries.Today(b, at2pm).Select(i => i.Title[..1]));
        // At 10:00 E's deadline has not passed yet: it sorts by its time with the rest.
        Assert.Equal(new[] { "A", "B", "E", "C", "D" }, BoardQueries.Today(b, Now).Select(i => i.Title[..1]));
    }

    [Fact]
    public void Overdue_IsPastDeadlinesOnly()
    {
        var (b, _) = Fixture();
        Assert.Equal(new[] { "A" }, BoardQueries.Overdue(b, Now).Select(i => i.Title[..1]));
        Assert.Equal(new[] { "A", "E" }, BoardQueries.Overdue(b, new DateTime(2026, 10, 9, 14, 0, 0)).Select(i => i.Title[..1]));
    }

    [Fact]
    public void Upcoming_GroupsTheNextDays_InOrder()
    {
        var (b, _) = Fixture();
        var up = BoardQueries.Upcoming(b, Now, 7);
        Assert.Equal(new[] { D(10, 10), D(10, 12) }, up.Select(d => d.Day));
        Assert.Equal(new[] { "L", "F" }, up[0].Items.Select(i => i.Title[..1]));   // timed first
        Assert.Equal(new[] { "G" }, up[1].Items.Select(i => i.Title[..1]));
        Assert.Contains(BoardQueries.Upcoming(b, Now, 14), d => d.Day == D(10, 20));
    }

    [Fact]
    public void Unscheduled_TaggedAndPlannedMinutes()
    {
        var (b, card) = Fixture();
        Assert.Equal(new[] { "K" }, BoardQueries.Unscheduled(b).Select(i => i.Title[..1]));
        card("F").Tags.Add("vfx"); card("F").EstimateMinutes = 30;
        card("L").EstimateMinutes = 15;
        Assert.Equal(new[] { "F" }, BoardQueries.Tagged(b, "#VFX").Select(i => i.Title[..1]));
        Assert.Equal(45, BoardQueries.PlannedMinutes(b, D(10, 10)));
    }

    // ------------------------------------------------------------------ recurrence

    [Theory]
    [InlineData("daily", 10, 9, 10, 9, 10, 10)]
    [InlineData("weekdays", 10, 9, 10, 9, 10, 12)]          // Friday -> Monday
    [InlineData("weekly", 10, 9, 10, 5, 10, 12)]            // anchored on a Monday
    [InlineData("weekly:mon,thu", 10, 12, 10, 12, 10, 15)]
    [InlineData("weekly:mon,thu", 10, 15, 10, 12, 10, 19)]
    [InlineData("days:3", 10, 9, 10, 1, 10, 10)]            // 1, 4, 7, 10
    [InlineData("weeks:2", 10, 9, 10, 2, 10, 16)]
    [InlineData("monthly", 10, 9, 9, 15, 10, 15)]
    public void Recurrence_Next(string text, int am, int ad, int anm, int and, int em, int ed)
    {
        Assert.True(Recurrence.TryParse(text, out var r));
        Assert.Equal(text, r.ToString());
        Assert.Equal(D(em, ed), r.Next(D(am, ad), D(anm, and)));
    }

    [Fact]
    public void Recurrence_Monthly_ClampsToShortMonths_AndKeepsTheAnchorDay()
    {
        Assert.True(Recurrence.TryParse("monthly", out var r));
        var jan31 = new DateOnly(2027, 1, 31);
        Assert.Equal(new DateOnly(2027, 2, 28), r.Next(jan31, jan31));
        Assert.Equal(new DateOnly(2027, 3, 31), r.Next(new DateOnly(2027, 2, 28), jan31));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hourly")]
    [InlineData("weekly:")]
    [InlineData("weekly:funday")]
    [InlineData("days:0")]
    [InlineData("days:x")]
    public void Recurrence_RejectsWhatItCannotRead(string text)
    {
        Assert.False(Recurrence.TryParse(text, out _));
    }

    [Fact]
    public void Complete_ARepeatingCard_SchedulesTheNextOne_AfterToday()
    {
        var b = new PrismaBoard();
        var it = b.Add(PrismaBoard.Kind.Task, "Backup", priority: 1, criterion: "backup file exists");
        it.Recurrence = "daily";
        it.Scheduled = D(10, 6);                  // three days overdue
        it.ScheduledTime = new TimeOnly(9, 0);
        it.Due = D(10, 7);                         // one day after the planned day
        it.Tags.Add("ops");
        it.EstimateMinutes = 20;
        it.Checklist.Add(new PrismaBoard.ChecklistEntry { Text = "copy", Done = true });

        var next = BoardCommands.Complete(b, it, Now)!;
        Assert.Equal(PrismaBoard.Status.Done, it.State);
        Assert.NotNull(it.CompletedAt);
        Assert.Equal(PrismaBoard.Status.Todo, next.State);
        Assert.NotEqual(it.Id, next.Id);
        Assert.NotEqual(it.Uid, next.Uid);
        Assert.Equal(D(10, 10), next.Scheduled);  // tomorrow, not 10-07
        Assert.Equal(new TimeOnly(9, 0), next.ScheduledTime);
        Assert.Equal(D(10, 11), next.Due);        // the same one-day gap
        Assert.Equal(new[] { "ops" }, next.Tags);
        Assert.Equal(20, next.EstimateMinutes);
        Assert.Equal(1, next.Priority);
        Assert.Equal("backup file exists", next.Criterion);
        Assert.False(Assert.Single(next.Checklist).Done);
        Assert.Contains(next.Notes, n => n.Contains("repeats " + it.Id));
    }

    [Fact]
    public void Complete_EarlyWeekly_MovesToTheFollowingWeek_AndAPlainCardDoesNotRepeat()
    {
        var b = new PrismaBoard();
        var weekly = b.Add(PrismaBoard.Kind.Task, "Retro");
        weekly.Recurrence = "weekly";
        weekly.Scheduled = D(10, 12);             // next Monday, done early on Friday
        Assert.Equal(D(10, 19), BoardCommands.Complete(b, weekly, Now)!.Scheduled);

        var dueOnly = b.Add(PrismaBoard.Kind.Task, "Report");
        dueOnly.Recurrence = "monthly";
        dueOnly.Due = D(10, 9);
        var nextReport = BoardCommands.Complete(b, dueOnly, Now)!;
        Assert.Equal(D(11, 9), nextReport.Due);
        Assert.Null(nextReport.Scheduled);

        var plain = b.Add(PrismaBoard.Kind.Task, "Once");
        Assert.Null(BoardCommands.Complete(b, plain, Now));
        Assert.Equal(PrismaBoard.Status.Done, plain.State);
    }

    [Fact]
    public void LeavingDone_ClearsCompletedAt()
    {
        var b = new PrismaBoard();
        var it = b.Add(PrismaBoard.Kind.Task, "x");
        b.Move(it, PrismaBoard.Status.Done);
        Assert.NotNull(it.CompletedAt);
        b.Move(it, PrismaBoard.Status.Todo);
        Assert.Null(it.CompletedAt);
    }
}

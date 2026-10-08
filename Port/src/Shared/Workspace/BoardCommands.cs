#nullable enable
using System;
using System.Linq;
using static Prisma.PrismaBoard;

namespace Prisma.Workspace
{
    /// <summary>
    /// The scheduler's write side: every change a view makes goes through here, so each one
    /// stamps <c>Updated</c> the same way. That matters for merging, and later for sync and the activity feed.
    /// Callers save the board afterwards (<see cref="PrismaBoard.Save(string?)"/>).
    /// </summary>
    public static class BoardCommands
    {
        /// <summary>Adds a card from a quick-add line (<see cref="QuickAdd"/>). Null when the line has no title.</summary>
        public static Item? AddFromQuickAdd(PrismaBoard board, string line, DateTime now, string source = "user", Status state = Status.Todo)
        {
            var q = QuickAdd.Parse(line, now);
            if (q.Title.Length == 0) return null;
            var item = board.Add(q.Type, q.Title, "", source, state, q.Priority ?? 2, criterion: q.Criterion);
            item.Scheduled = q.Scheduled;
            item.ScheduledTime = q.ScheduledTime;
            item.Due = q.Due;
            item.DueTime = q.DueTime;
            item.Tags = q.Tags.ToList();
            item.Assignee = q.Assignee;
            item.EstimateMinutes = q.EstimateMinutes;
            item.Recurrence = q.Recurrence;
            return item;
        }

        /// <summary>Plans a card for a day (null clears it).</summary>
        public static void Schedule(Item item, DateOnly? day, TimeOnly? time = null)
        {
            item.Scheduled = day;
            item.ScheduledTime = day == null ? null : time;
            Touch(item);
        }

        public static void SetDue(Item item, DateOnly? day, TimeOnly? time = null)
        {
            item.Due = day;
            item.DueTime = day == null ? null : time;
            Touch(item);
        }

        /// <summary>Plans a card for tomorrow, keeping its time of day.</summary>
        public static void SnoozeToTomorrow(Item item, DateTime now) => Schedule(item, DateOnly.FromDateTime(now).AddDays(1), item.ScheduledTime);

        public static void Touch(Item item) => item.Updated = DateTime.UtcNow;

        /// <summary>
        /// Marks a card done. A repeating card then gets its next occurrence as a new TO DO card: the
        /// same title, tags, assignee, estimate, criterion and rule, the checklist unticked, and its planned
        /// day (and deadline, by the same gap) moved to the rule's next date after today or after its own
        /// date, whichever is later. Returns that new card, or null.
        /// </summary>
        public static Item? Complete(PrismaBoard board, Item item, DateTime now, string? note = "done")
        {
            board.Move(item, Status.Done, note);
            if (!Recurrence.TryParse(item.Recurrence, out var rule)) return null;

            var today = DateOnly.FromDateTime(now);
            var anchor = item.Day ?? today;
            var from = anchor > today ? anchor : today;
            var next = rule.Next(from, anchor);

            var copy = board.Add(item.Type, item.Title, item.Detail, item.Source, Status.Todo, item.Priority, criterion: item.Criterion);
            copy.Tags = item.Tags.ToList();
            copy.Assignee = item.Assignee;
            copy.EstimateMinutes = item.EstimateMinutes;
            copy.Recurrence = item.Recurrence;
            copy.Checklist = item.Checklist.Select(c => new ChecklistEntry { Text = c.Text, Done = false }).ToList();
            if (item.Scheduled is { } s)
            {
                copy.Scheduled = next;
                copy.ScheduledTime = item.ScheduledTime;
                if (item.Due is { } d) { copy.Due = next.AddDays(d.DayNumber - s.DayNumber); copy.DueTime = item.DueTime; }
            }
            else if (item.Due != null)
            {
                copy.Due = next;
                copy.DueTime = item.DueTime;
            }
            else copy.Scheduled = next;
            copy.Notes.Add($"{DateTime.Now:yyyy-MM-dd HH:mm} repeats {item.Id} ({rule})");
            return copy;
        }
    }
}

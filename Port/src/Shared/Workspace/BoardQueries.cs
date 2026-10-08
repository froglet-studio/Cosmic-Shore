#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using static Prisma.PrismaBoard;

namespace Prisma.Workspace
{
    /// <summary>
    /// The scheduler's read side. Pure functions of a board and "now" (the user's local clock).
    /// Only open cards count: TO DO and DOING, never suggestions, DONE or dismissed ones.
    /// </summary>
    public static class BoardQueries
    {
        /// <summary>Open cards whose deadline has passed: an earlier day, or today at a time already gone.</summary>
        public static List<Item> Overdue(PrismaBoard board, DateTime now)
        {
            var today = DateOnly.FromDateTime(now);
            var clock = TimeOnly.FromDateTime(now);
            return board.Items.Where(i => i.IsOpen && IsOverdue(i, today, clock)).OrderBy(i => i.Due).ThenBy(i => i.DueTime ?? TimeOnly.MaxValue).ThenBy(i => i.Priority).ToList();
        }

        public static bool IsOverdue(Item i, DateOnly today, TimeOnly clock) =>
            i.Due is { } due && (due < today || (due == today && i.DueTime is { } t && t < clock));

        /// <summary>
        /// What to do today: open cards planned or due today or earlier (carried over). Overdue first,
        /// then by time of day (untimed after timed), then priority, then key.
        /// </summary>
        public static List<Item> Today(PrismaBoard board, DateTime now)
        {
            var today = DateOnly.FromDateTime(now);
            var clock = TimeOnly.FromDateTime(now);
            return board.Items
                .Where(i => i.IsOpen && ((i.Scheduled is { } s && s <= today) || (i.Due is { } d && d <= today)))
                .OrderBy(i => IsOverdue(i, today, clock) ? 0 : 1)
                .ThenBy(i => TodayTime(i, today) ?? TimeOnly.MaxValue)
                .ThenBy(i => i.Priority)
                .ThenBy(i => i.Id, StringComparer.Ordinal)
                .ToList();
        }

        static TimeOnly? TodayTime(Item i, DateOnly today) =>
            i.Scheduled == today ? i.ScheduledTime : i.Due == today ? i.DueTime : null;

        /// <summary>
        /// The next <paramref name="days"/> days after today, one entry per day that has cards (in
        /// date order). A card sits on its planned day, or its deadline when it has no planned day.
        /// </summary>
        public static List<(DateOnly Day, List<Item> Items)> Upcoming(PrismaBoard board, DateTime now, int days = 7)
        {
            var today = DateOnly.FromDateTime(now);
            var last = today.AddDays(days);
            return board.Items
                .Where(i => i.IsOpen && i.Day is { } d && d > today && d <= last)
                .GroupBy(i => i.Day!.Value)
                .OrderBy(g => g.Key)
                .Select(g => (g.Key, g.OrderBy(i => i.Time ?? TimeOnly.MaxValue).ThenBy(i => i.Priority).ThenBy(i => i.Id, StringComparer.Ordinal).ToList()))
                .ToList();
        }

        /// <summary>Open cards with no day at all: the backlog.</summary>
        public static List<Item> Unscheduled(PrismaBoard board) =>
            board.Items.Where(i => i.IsOpen && i.Day == null).OrderBy(i => i.Priority).ThenByDescending(i => i.Updated).ToList();

        /// <summary>Open cards carrying a tag (case-insensitive, with or without '#').</summary>
        public static List<Item> Tagged(PrismaBoard board, string tag)
        {
            tag = tag.TrimStart('#').ToLowerInvariant();
            return board.Items.Where(i => i.IsOpen && i.Tags.Contains(tag)).OrderBy(i => i.Priority).ToList();
        }

        /// <summary>Minutes of estimated work planned for a day (cards without an estimate count 0).</summary>
        public static int PlannedMinutes(PrismaBoard board, DateOnly day) =>
            board.Items.Where(i => i.IsOpen && i.Day == day).Sum(i => i.EstimateMinutes ?? 0);
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Prisma.Workspace
{
    /// <summary>
    /// A repeat rule, stored on a card as text:
    /// <c>daily</c>, <c>weekdays</c> (Mon-Fri), <c>weekly</c> (the card's own weekday),
    /// <c>weekly:mon,thu</c>, <c>monthly</c> (the card's day of month, clamped to short months),
    /// <c>days:N</c> and <c>weeks:N</c> (every N days or weeks from the card's date).
    /// </summary>
    public sealed class Recurrence
    {
        public enum Unit { Daily, Weekdays, Weekly, Monthly, EveryDays, EveryWeeks }

        public Unit Kind { get; }
        public int Interval { get; }
        public IReadOnlyList<DayOfWeek> Days { get; }

        Recurrence(Unit kind, int interval = 1, IReadOnlyList<DayOfWeek>? days = null)
        {
            Kind = kind; Interval = Math.Max(1, interval); Days = days ?? Array.Empty<DayOfWeek>();
        }

        public static Recurrence Daily => new(Unit.Daily);
        public static Recurrence Weekdays => new(Unit.Weekdays);
        public static Recurrence Weekly(params DayOfWeek[] days) => new(Unit.Weekly, 1, days.Distinct().OrderBy(d => ((int)d + 6) % 7).ToArray());
        public static Recurrence Monthly => new(Unit.Monthly);
        public static Recurrence EveryDays(int n) => n == 1 ? Daily : new(Unit.EveryDays, n);
        public static Recurrence EveryWeeks(int n) => n == 1 ? Weekly() : new(Unit.EveryWeeks, n);

        public static bool TryParse(string? text, out Recurrence rule)
        {
            rule = Daily;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var t = text.Trim().ToLowerInvariant();
            switch (t)
            {
                case "daily": rule = Daily; return true;
                case "weekdays": rule = Weekdays; return true;
                case "weekly": rule = Weekly(); return true;
                case "monthly": rule = Monthly; return true;
            }
            if (t.StartsWith("weekly:"))
            {
                var days = new List<DayOfWeek>();
                foreach (var d in t[7..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!DateWords.TryWeekday(d, allowAmbiguous: true, out var day)) return false;
                    days.Add(day);
                }
                if (days.Count == 0) return false;
                rule = Weekly(days.ToArray());
                return true;
            }
            if ((t.StartsWith("days:") || t.StartsWith("weeks:")) && int.TryParse(t[(t.IndexOf(':') + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1)
            {
                rule = t.StartsWith("days:") ? EveryDays(n) : EveryWeeks(n);
                return true;
            }
            return false;
        }

        public override string ToString() => Kind switch
        {
            Unit.Daily => "daily",
            Unit.Weekdays => "weekdays",
            Unit.Weekly => Days.Count == 0 ? "weekly" : "weekly:" + string.Join(",", Days.Select(DateWords.Short)),
            Unit.Monthly => "monthly",
            Unit.EveryDays => $"days:{Interval}",
            _ => $"weeks:{Interval}",
        };

        /// <summary>The first occurrence strictly after <paramref name="after"/>. <paramref name="anchor"/> is the card's own date: it fixes the weekday, day of month and N-day phase.</summary>
        public DateOnly Next(DateOnly after, DateOnly anchor)
        {
            switch (Kind)
            {
                case Unit.Daily: return after.AddDays(1);
                case Unit.Weekdays:
                {
                    var d = after.AddDays(1);
                    while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
                    return d;
                }
                case Unit.Weekly:
                {
                    var days = Days.Count > 0 ? Days : new[] { anchor.DayOfWeek };
                    var d = after.AddDays(1);
                    while (!days.Contains(d.DayOfWeek)) d = d.AddDays(1);
                    return d;
                }
                case Unit.EveryDays:
                case Unit.EveryWeeks:
                {
                    int step = Kind == Unit.EveryDays ? Interval : Interval * 7;
                    int gap = after.DayNumber - anchor.DayNumber;
                    int k = gap < 0 ? 0 : gap / step + 1;
                    return anchor.AddDays(k * step);
                }
                default: // Monthly
                {
                    for (int m = 0; ; m++)
                    {
                        var month = new DateOnly(after.Year, after.Month, 1).AddMonths(m);
                        var d = new DateOnly(month.Year, month.Month, Math.Min(anchor.Day, DateTime.DaysInMonth(month.Year, month.Month)));
                        if (d > after) return d;
                    }
                }
            }
        }

        /// <summary>The first occurrence on or after <paramref name="day"/> (where a new repeating card starts).</summary>
        public DateOnly FirstOnOrAfter(DateOnly day, DateOnly? anchor = null) => Next(day.AddDays(-1), anchor ?? day);
    }
}

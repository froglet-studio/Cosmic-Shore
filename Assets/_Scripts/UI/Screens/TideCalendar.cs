using System;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Tide's clock (Loop Atlas <c>era.tide</c>, Garrett 2026-11-20): the long game turns every few weeks, and Home
    /// shows where the current Tide stands. Pure arithmetic over one authored start date, so every device agrees on the
    /// week without a server: Tide 1 began at <c>start</c>, each Tide lasts <c>weeks</c> weeks, and the week a moment
    /// falls in is counted from there. A moment before the start reads as the first week of the first Tide.
    /// </summary>
    public static class TideCalendar
    {
        public readonly struct TideWeek
        {
            /// <summary>The Tide, counted from 1.</summary>
            public readonly int Tide;
            /// <summary>The week inside the Tide, from 1 to <see cref="Weeks"/>.</summary>
            public readonly int Week;
            /// <summary>Weeks in a Tide.</summary>
            public readonly int Weeks;
            /// <summary>How far through this week the moment is, from 0 to 1.</summary>
            public readonly float WeekFraction;
            /// <summary>Time until the Tide turns (the next Tide begins).</summary>
            public readonly TimeSpan UntilTurn;

            public TideWeek(int tide, int week, int weeks, float weekFraction, TimeSpan untilTurn)
            {
                Tide = tide;
                Week = week;
                Weeks = weeks;
                WeekFraction = weekFraction;
                UntilTurn = untilTurn;
            }
        }

        const double DaysPerWeek = 7.0;

        /// <summary>Where <paramref name="utcNow"/> falls in the Tides that began at <paramref name="startUtc"/>.</summary>
        public static TideWeek At(DateTime utcNow, DateTime startUtc, int weeks)
        {
            weeks = Math.Max(1, weeks);
            double days = Math.Max(0.0, (utcNow - startUtc).TotalDays);
            double tideDays = weeks * DaysPerWeek;
            int tideIndex = (int)Math.Floor(days / tideDays);
            double intoTide = days - tideIndex * tideDays;
            int weekIndex = Math.Min(weeks - 1, (int)Math.Floor(intoTide / DaysPerWeek));
            float fraction = (float)((intoTide - weekIndex * DaysPerWeek) / DaysPerWeek);
            var untilTurn = TimeSpan.FromDays(tideDays - intoTide);
            return new TideWeek(tideIndex + 1, weekIndex + 1, weeks, Math.Clamp(fraction, 0f, 1f), untilTurn);
        }

        /// <summary>
        /// The authored start date ("yyyy-MM-dd", UTC). An empty or unreadable value is null, and Home hides the clock
        /// rather than guess one.
        /// </summary>
        public static DateTime? ParseStart(string isoDate)
        {
            if (string.IsNullOrWhiteSpace(isoDate)) return null;
            return DateTime.TryParseExact(isoDate.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var start)
                ? start
                : (DateTime?)null;
        }

        /// <summary>
        /// The quests a chain of <paramref name="slots"/> links shows around the current one: as many behind it as fit
        /// while it still shows what comes next. Returns the first quest's index; -1 when there is nothing to show.
        /// </summary>
        public static int ChainStart(int questCount, int current, int slots)
        {
            if (questCount <= 0 || slots <= 0 || current < 0) return -1;
            int start = current - (slots - 1) / 2;
            return Math.Max(0, Math.Min(start, questCount - slots));
        }
    }
}

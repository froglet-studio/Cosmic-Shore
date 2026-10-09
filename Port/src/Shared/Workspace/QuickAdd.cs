#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Prisma.Workspace
{
    /// <summary>What one quick-add line asked for. Fields not mentioned stay null or empty.</summary>
    public sealed class QuickAddResult
    {
        public string Title { get; set; } = "";
        public PrismaBoard.Kind Type { get; set; } = PrismaBoard.Kind.Task;
        public int? Priority { get; set; }
        public List<string> Tags { get; } = new();
        public string? Assignee { get; set; }
        public DateOnly? Scheduled { get; set; }
        public TimeOnly? ScheduledTime { get; set; }
        public DateOnly? Due { get; set; }
        public TimeOnly? DueTime { get; set; }
        public int? EstimateMinutes { get; set; }
        public string? Recurrence { get; set; }
        public string Criterion { get; set; } = "";
    }

    /// <summary>
    /// Reads one line the way people type a task:
    /// <c>Fix trail tomorrow 3pm #vfx !1 ~45m @yash done when: no warnings in SkimRace</c>.
    /// <list type="bullet">
    /// <item><c>#tag</c> adds a tag, <c>@name</c> sets the assignee, <c>!1</c>/<c>!2</c>/<c>!3</c> (or <c>!high</c>/<c>!low</c>) sets the priority.</item>
    /// <item><c>~45m</c>, <c>~2h</c>, <c>~1h30m</c>, <c>~1.5h</c> or <c>~45</c> (minutes) sets the estimate.</item>
    /// <item>Days are the planned day: <c>today</c>, <c>tomorrow</c>/<c>tmrw</c>, a weekday (the next one, today included),
    /// <c>next fri</c> (strictly after today), <c>next week</c> (next Monday), <c>in 3 days</c> / <c>in 2 weeks</c>,
    /// <c>2026-10-12</c>, <c>oct 12</c> / <c>12 oct</c> (next year if already past). An optional <c>on</c> before the day is absorbed.</item>
    /// <item><c>due</c> + a day sets the deadline instead.</item>
    /// <item>Times: <c>3pm</c>, <c>3:30pm</c>, <c>3 pm</c>, <c>15:00</c>, optionally after <c>at</c>. A time attaches to the day before it; with no day,
    /// it is today, or tomorrow if that time has passed.</item>
    /// <item><c>every day</c>, <c>every weekday</c>, <c>every week</c>, <c>every month</c>, <c>every mon,thu</c>, <c>every 2 days</c> / <c>every 3 weeks</c>
    /// repeats it. With no day given, it starts at the first occurrence from today.</item>
    /// <item>A leading <c>bug:</c> or <c>task:</c> sets the kind, and <c>done when: ...</c> at the end is the acceptance criterion.</item>
    /// </list>
    /// Everything else is the title. To keep titles intact, <c>sat</c>, <c>sun</c> and month names count as dates only where a date is
    /// expected (after <c>on</c>/<c>due</c>/<c>next</c>/<c>every</c>, or with a day number).
    /// </summary>
    public static class QuickAdd
    {
        static readonly Regex Estimate = new(@"^~(?:(?<h>\d+(?:\.\d+)?)h)?(?:(?<m>\d+)(?:m|min|mins)?)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex Clock24 = new(@"^(?<h>[01]?\d|2[0-3]):(?<m>[0-5]\d)$", RegexOptions.CultureInvariant);
        static readonly Regex Clock12 = new(@"^(?<h>1[0-2]|0?[1-9])(?::(?<m>[0-5]\d))?(?<ap>am|pm)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex Hour12 = new(@"^(?<h>1[0-2]|0?[1-9])(?::(?<m>[0-5]\d))?$", RegexOptions.CultureInvariant);
        static readonly Regex DoneWhen = new(@"\bdone\s+when\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static QuickAddResult Parse(string text, DateTime now)
        {
            var r = new QuickAddResult();
            text ??= "";
            var dw = DoneWhen.Match(text);
            if (dw.Success)
            {
                r.Criterion = text[(dw.Index + dw.Length)..].Trim();
                text = text[..dw.Index];
            }
            text = text.Trim();
            if (text.StartsWith("bug:", StringComparison.OrdinalIgnoreCase)) { r.Type = PrismaBoard.Kind.Bug; text = text[4..]; }
            else if (text.StartsWith("task:", StringComparison.OrdinalIgnoreCase)) { text = text[5..]; }

            var today = DateOnly.FromDateTime(now);
            var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var used = new bool[tokens.Length];
            string Low(int i) => i < tokens.Length ? tokens[i].TrimEnd(',', '.', ';').ToLowerInvariant() : "";
            bool lastWasDue = false;
            TimeOnly? looseTime = null;

            for (int i = 0; i < tokens.Length; i++)
            {
                if (used[i]) continue;
                var raw = tokens[i].TrimEnd(',', '.', ';');
                var w = Low(i);

                if (raw.Length > 1 && raw[0] == '#') { var tag = raw[1..].ToLowerInvariant(); if (!r.Tags.Contains(tag)) r.Tags.Add(tag); used[i] = true; continue; }
                if (raw.Length > 1 && raw[0] == '@') { r.Assignee = raw[1..]; used[i] = true; continue; }
                if (w is "!1" or "!p1" or "!high") { r.Priority = 1; used[i] = true; continue; }
                if (w is "!2" or "!p2" or "!normal") { r.Priority = 2; used[i] = true; continue; }
                if (w is "!3" or "!p3" or "!low") { r.Priority = 3; used[i] = true; continue; }
                if (w.Length > 1 && w[0] == '~' && Estimate.Match(w) is { Success: true } em && (em.Groups["h"].Success || em.Groups["m"].Success))
                {
                    double h = em.Groups["h"].Success ? double.Parse(em.Groups["h"].Value, CultureInfo.InvariantCulture) : 0;
                    int m = em.Groups["m"].Success ? int.Parse(em.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
                    r.EstimateMinutes = (int)Math.Round(h * 60) + m;
                    used[i] = true; continue;
                }
                if (w == "every" && TryRepeat(tokens, i + 1, out var rule, out int rn))
                {
                    r.Recurrence = rule.ToString();
                    for (int k = i; k <= i + rn; k++) used[k] = true;
                    i += rn; continue;
                }
                if (w == "due" && TryDate(tokens, i + 1, today, strict: false, out var due, out int dn))
                {
                    r.Due = due; lastWasDue = true;
                    for (int k = i; k <= i + dn; k++) used[k] = true;
                    i += dn; continue;
                }
                if (w == "on" && r.Scheduled == null && TryDate(tokens, i + 1, today, strict: false, out var on, out int onN))
                {
                    r.Scheduled = on; lastWasDue = false;
                    for (int k = i; k <= i + onN; k++) used[k] = true;
                    i += onN; continue;
                }
                if (r.Scheduled == null && TryDate(tokens, i, today, strict: true, out var day, out int n))
                {
                    r.Scheduled = day; lastWasDue = false;
                    for (int k = i; k < i + n; k++) used[k] = true;
                    i += n - 1; continue;
                }
                int at = w == "at" ? 1 : 0;
                if (TryTime(tokens, i + at, out var time, out int tn))
                {
                    if (lastWasDue && r.Due != null && r.DueTime == null) r.DueTime = time;
                    else if (r.Scheduled != null && r.ScheduledTime == null) r.ScheduledTime = time;
                    else if (r.Scheduled == null && looseTime == null) looseTime = time;
                    else continue;   // a second time: leave it in the title
                    for (int k = i; k < i + at + tn; k++) used[k] = true;
                    i += at + tn - 1; continue;
                }
            }

            if (looseTime != null)
            {
                r.Scheduled ??= looseTime.Value > TimeOnly.FromDateTime(now) ? today : today.AddDays(1);
                r.ScheduledTime = looseTime;
            }
            if (r.Recurrence != null && r.Scheduled == null && r.Due == null && Workspace.Recurrence.TryParse(r.Recurrence, out var rec))
                r.Scheduled = rec.FirstOnOrAfter(today);

            r.Title = string.Join(" ", tokens.Where((_, i) => !used[i])).Trim();
            return r;
        }

        static bool TryRepeat(string[] t, int i, out Recurrence rule, out int consumed)
        {
            rule = Recurrence.Daily; consumed = 0;
            string W(int k) => k < t.Length ? t[k].TrimEnd(',', '.', ';').ToLowerInvariant() : "";
            var w = W(i);
            switch (w)
            {
                case "day": rule = Recurrence.Daily; consumed = 1; return true;
                case "weekday": case "weekdays": rule = Recurrence.Weekdays; consumed = 1; return true;
                case "week": rule = Recurrence.Weekly(); consumed = 1; return true;
                case "month": rule = Recurrence.Monthly; consumed = 1; return true;
            }
            if (int.TryParse(w, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1)
            {
                var unit = W(i + 1);
                if (unit is "day" or "days") { rule = Recurrence.EveryDays(n); consumed = 2; return true; }
                if (unit is "week" or "weeks") { rule = Recurrence.EveryWeeks(n); consumed = 2; return true; }
                return false;
            }
            // every mon,thu / every mon thu / every mon and thu
            var days = new List<DayOfWeek>();
            int k = i;
            while (k < t.Length)
            {
                var parts = W(k).Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || !parts.All(p => DateWords.TryWeekday(p, allowAmbiguous: true, out _)))
                {
                    if (W(k) == "and" && days.Count > 0 && k + 1 < t.Length && DateWords.TryWeekday(W(k + 1), true, out _)) { k++; continue; }
                    break;
                }
                foreach (var p in parts) { DateWords.TryWeekday(p, true, out var d); days.Add(d); }
                k++;
            }
            if (days.Count == 0) return false;
            rule = Recurrence.Weekly(days.ToArray());
            consumed = k - i;
            return true;
        }

        /// <summary>A day starting at token i. strict: only forms that are unlikely to be part of a title.</summary>
        static bool TryDate(string[] t, int i, DateOnly today, bool strict, out DateOnly day, out int consumed)
        {
            day = today; consumed = 0;
            string W(int k) => k < t.Length ? t[k].TrimEnd(',', '.', ';').ToLowerInvariant() : "";
            var w = W(i);
            if (w.Length == 0) return false;
            switch (w)
            {
                case "today": case "tod": day = today; consumed = 1; return true;
                case "tomorrow": case "tmrw": case "tmr": day = today.AddDays(1); consumed = 1; return true;
            }
            if (w == "next")
            {
                var n = W(i + 1);
                if (n == "week") { day = DateWords.NextWeekday(today, DayOfWeek.Monday, includeToday: false); consumed = 2; return true; }
                if (DateWords.TryWeekday(n, allowAmbiguous: true, out var nd)) { day = DateWords.NextWeekday(today, nd, includeToday: false); consumed = 2; return true; }
                return false;
            }
            if (w == "in" && int.TryParse(W(i + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count >= 0)
            {
                var unit = W(i + 2);
                if (unit is "day" or "days") { day = today.AddDays(count); consumed = 3; return true; }
                if (unit is "week" or "weeks") { day = today.AddDays(7 * count); consumed = 3; return true; }
                return false;
            }
            if (DateWords.TryWeekday(w, allowAmbiguous: !strict, out var wd)) { day = DateWords.NextWeekday(today, wd, includeToday: true); consumed = 1; return true; }
            if (DateOnly.TryParseExact(w, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso)) { day = iso; consumed = 1; return true; }
            // oct 12 / 12 oct
            if (DateWords.TryMonth(w, out var mon) && TryDayNumber(W(i + 1), out var dn) && TryMonthDay(today, mon, dn, out day)) { consumed = 2; return true; }
            if (TryDayNumber(w, out var dn2) && DateWords.TryMonth(W(i + 1), out var mon2) && TryMonthDay(today, mon2, dn2, out day)) { consumed = 2; return true; }
            return false;
        }

        static bool TryDayNumber(string w, out int d)
        {
            w = w.TrimEnd('s', 't', 'n', 'd', 'r', 'h'); // 1st 2nd 3rd 12th
            return int.TryParse(w, NumberStyles.None, CultureInfo.InvariantCulture, out d) && d is >= 1 and <= 31;
        }

        static bool TryMonthDay(DateOnly today, int month, int dayOfMonth, out DateOnly day)
        {
            day = today;
            if (dayOfMonth > DateTime.DaysInMonth(today.Year, month) && !(month == 2 && dayOfMonth == 29)) return false;
            for (int y = today.Year; y <= today.Year + 4; y++)
            {
                if (dayOfMonth > DateTime.DaysInMonth(y, month)) continue;
                var d = new DateOnly(y, month, dayOfMonth);
                if (d >= today) { day = d; return true; }
            }
            return false;
        }

        static bool TryTime(string[] t, int i, out TimeOnly time, out int consumed)
        {
            time = default; consumed = 0;
            if (i >= t.Length) return false;
            var w = t[i].TrimEnd(',', '.', ';').ToLowerInvariant();
            if (w == "noon") { time = new TimeOnly(12, 0); consumed = 1; return true; }
            var m = Clock12.Match(w);
            if (m.Success) { time = To24(m.Groups["h"].Value, m.Groups["m"].Value, m.Groups["ap"].Value); consumed = 1; return true; }
            m = Clock24.Match(w);
            if (m.Success) { time = new TimeOnly(int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture)); consumed = 1; return true; }
            if (i + 1 < t.Length)
            {
                var ap = t[i + 1].TrimEnd(',', '.', ';').ToLowerInvariant();
                m = Hour12.Match(w);
                if (m.Success && ap is "am" or "pm") { time = To24(m.Groups["h"].Value, m.Groups["m"].Value, ap); consumed = 2; return true; }
            }
            return false;
        }

        static TimeOnly To24(string h, string m, string ap)
        {
            int hour = int.Parse(h, CultureInfo.InvariantCulture) % 12;
            if (ap.Equals("pm", StringComparison.OrdinalIgnoreCase)) hour += 12;
            return new TimeOnly(hour, m.Length > 0 ? int.Parse(m, CultureInfo.InvariantCulture) : 0);
        }
    }

    /// <summary>Weekday and month words.</summary>
    public static class DateWords
    {
        static readonly Dictionary<string, DayOfWeek> Weekdays = new()
        {
            ["monday"] = DayOfWeek.Monday, ["mon"] = DayOfWeek.Monday,
            ["tuesday"] = DayOfWeek.Tuesday, ["tue"] = DayOfWeek.Tuesday, ["tues"] = DayOfWeek.Tuesday,
            ["wednesday"] = DayOfWeek.Wednesday, ["wed"] = DayOfWeek.Wednesday,
            ["thursday"] = DayOfWeek.Thursday, ["thu"] = DayOfWeek.Thursday, ["thur"] = DayOfWeek.Thursday, ["thurs"] = DayOfWeek.Thursday,
            ["friday"] = DayOfWeek.Friday, ["fri"] = DayOfWeek.Friday,
            ["saturday"] = DayOfWeek.Saturday, ["sat"] = DayOfWeek.Saturday,
            ["sunday"] = DayOfWeek.Sunday, ["sun"] = DayOfWeek.Sunday,
        };
        /// <summary>Short forms that are also ordinary words; dates only where a date is expected.</summary>
        static readonly HashSet<string> Ambiguous = new() { "sat", "sun" };

        static readonly string[] Months = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

        public static bool TryWeekday(string w, bool allowAmbiguous, out DayOfWeek day)
        {
            w = w.ToLowerInvariant();
            if (Weekdays.TryGetValue(w, out day) && (allowAmbiguous || !Ambiguous.Contains(w))) return true;
            day = default;
            return false;
        }

        public static string Short(DayOfWeek d) => d.ToString()[..3].ToLowerInvariant();

        /// <summary>Month number for "oct" / "october" / "sept".</summary>
        public static bool TryMonth(string w, out int month)
        {
            w = w.ToLowerInvariant();
            month = 0;
            if (w.Length < 3) return false;
            for (int i = 0; i < 12; i++)
            {
                var full = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames[i].ToLowerInvariant();
                if (w == Months[i] || w == full || (w == "sept" && i == 8)) { month = i + 1; return true; }
            }
            return false;
        }

        /// <summary>The next <paramref name="day"/> from <paramref name="from"/> (0-6 days ahead when today counts, else 1-7).</summary>
        public static DateOnly NextWeekday(DateOnly from, DayOfWeek day, bool includeToday)
        {
            int ahead = ((int)day - (int)from.DayOfWeek + 7) % 7;
            if (ahead == 0 && !includeToday) ahead = 7;
            return from.AddDays(ahead);
        }
    }
}

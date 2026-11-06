using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CosmicShore.Editor
{
    /// <summary>
    /// A platform's (or a record's) answer to "can we land?". The numbers are serialized in
    /// <see cref="LaunchConsoleConfigSO"/> (the audit verdicts), so they never change.
    /// </summary>
    public enum LaunchState
    {
        Unknown = 0,
        Go = 1,
        AtRisk = 2,
        NoGo = 3,
    }

    /// <summary>
    /// One record of the launch-status branch's <c>status.json</c>: what one pipeline last did
    /// (<c>Docs/LAUNCH/STATUS_CONTRACT.md</c>). The publisher refuses anything off-contract; this
    /// reader is lenient and keeps only the fields the console shows.
    /// </summary>
    public sealed class LaunchRecord
    {
        public string Id;
        public string Platform;
        public string Kind;
        public string Result;
        public string Source;
        public string Channel;
        public string Version;
        public string Build;
        public string Commit;
        public string Summary;
        public string RunUrl;
        public string Link;
        public string RollbackTo;
        /// <summary>When the record was published (<c>updated_at</c>), UTC.</summary>
        public DateTime? UpdatedAt;
        /// <summary>When the thing it records happened (<c>details.event_date</c>), UTC midnight, if given.</summary>
        public DateTime? EventDate;
        public readonly Dictionary<string, string> Details = new Dictionary<string, string>();

        /// <summary>The moment the record's figure is as of: its event date when it carries one, else its publish time.</summary>
        public DateTime? AsOf => EventDate ?? UpdatedAt;

        /// <summary>Version, build number and short commit in one line, or an em dash.</summary>
        public string VersionLine
        {
            get
            {
                var parts = new List<string>(3);
                if (!string.IsNullOrEmpty(Version)) parts.Add(Version);
                if (!string.IsNullOrEmpty(Build)) parts.Add("(" + Build + ")");
                if (!string.IsNullOrEmpty(Commit)) parts.Add(Commit.Length > 9 ? Commit.Substring(0, 9) : Commit);
                return parts.Count == 0 ? "—" : string.Join(" ", parts);
            }
        }

        /// <summary>The run, else the store or dashboard page; only a plain https URL, else null.</summary>
        public string OpenUrl => LaunchRules.SafeUrl(RunUrl) ?? LaunchRules.SafeUrl(Link);
    }

    /// <summary>The merged <c>status.json</c>: every component's latest record.</summary>
    public sealed class LaunchStatusBoard
    {
        public DateTime? GeneratedAt;
        public readonly Dictionary<string, LaunchRecord> Records = new Dictionary<string, LaunchRecord>(StringComparer.Ordinal);

        public LaunchRecord Get(string id) =>
            !string.IsNullOrEmpty(id) && Records.TryGetValue(id, out var r) ? r : null;

        /// <summary>
        /// Parses <c>status.json</c>. Returns null and an error line when the text is not a status
        /// board: a reader that cannot parse the file says so rather than showing an empty board as Go.
        /// </summary>
        public static LaunchStatusBoard Parse(string json, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json)) { error = "status.json is empty."; return null; }

            JToken root;
            try
            {
                // Dates stay strings: Newtonsoft's own date sniffing would turn "2026-10-13" into a
                // local-time DateTime on some machines and not on others.
                using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
                root = JToken.ReadFrom(reader);
            }
            catch (JsonException e)
            {
                error = "status.json is not valid JSON: " + e.Message;
                return null;
            }

            if (root is not JObject obj || obj["components"] is not JObject comps)
            {
                error = "status.json has no \"components\" object.";
                return null;
            }

            var board = new LaunchStatusBoard { GeneratedAt = ParseUtc(Str(obj, "generated_at")) };
            foreach (var prop in comps.Properties())
            {
                if (prop.Value is not JObject c) continue;
                var r = new LaunchRecord
                {
                    Id = Str(c, "id") ?? prop.Name,
                    Platform = Str(c, "platform"),
                    Kind = Str(c, "kind"),
                    Result = Str(c, "result"),
                    Source = Str(c, "source"),
                    Channel = Str(c, "channel"),
                    Version = Str(c, "version"),
                    Build = Str(c, "build"),
                    Commit = Str(c, "commit"),
                    Summary = Str(c, "summary"),
                    RunUrl = Str(c, "run_url"),
                    Link = Str(c, "link"),
                    RollbackTo = Str(c, "rollback_to"),
                    UpdatedAt = ParseUtc(Str(c, "updated_at")),
                };
                if (c["details"] is JObject details)
                    foreach (var d in details.Properties())
                        if (d.Value.Type != JTokenType.Object && d.Value.Type != JTokenType.Array)
                            r.Details[d.Name] = d.Value.ToString();
                if (r.Details.TryGetValue("event_date", out var ev)) r.EventDate = ParseUtc(ev);
                board.Records[prop.Name] = r;
            }
            return board;
        }

        static string Str(JObject o, string key)
        {
            var t = o[key];
            if (t == null || t.Type == JTokenType.Null) return null;
            var s = t.Type == JTokenType.String ? (string)t : t.ToString(Formatting.None);
            return string.IsNullOrEmpty(s) ? null : s;
        }

        /// <summary>An ISO-8601 time or a plain yyyy-MM-dd date, read as UTC.</summary>
        public static DateTime? ParseUtc(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var day))
                return DateTime.SpecifyKind(day, DateTimeKind.Utc);
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t))
                return t.UtcDateTime;
            return null;
        }
    }

    /// <summary>
    /// The pill rule, shared by the window and its tests. Kept free of Unity types so it is
    /// exactly what the EditMode tests exercise.
    /// </summary>
    public static class LaunchRules
    {
        /// <summary>Worse states rank higher; Unknown is below Go, so any evidence outranks none.</summary>
        public static int Rank(LaunchState s) => s switch
        {
            LaunchState.Go => 1,
            LaunchState.AtRisk => 2,
            LaunchState.NoGo => 3,
            _ => 0,
        };

        public static LaunchState Worst(LaunchState a, LaunchState b) => Rank(a) >= Rank(b) ? a : b;

        public static string Word(LaunchState s) => s switch
        {
            LaunchState.Go => "Go",
            LaunchState.AtRisk => "At risk",
            LaunchState.NoGo => "No-go",
            _ => "Unknown",
        };

        public static TimeSpan? Age(LaunchRecord r, DateTime nowUtc) =>
            r?.AsOf is DateTime t ? nowUtc - t : (TimeSpan?)null;

        public static bool IsStale(LaunchRecord r, DateTime nowUtc, double staleHours) =>
            Age(r, nowUtc) is TimeSpan a && a.TotalHours > staleHours;

        /// <summary>
        /// One record: a failure is No-go; a success is Go while fresh and At risk once older than
        /// <paramref name="staleHours"/> (or of unknown age); skipped, cancelled or running is At risk.
        /// </summary>
        public static LaunchState StateOf(LaunchRecord r, DateTime nowUtc, double staleHours)
        {
            if (r == null) return LaunchState.Unknown;
            switch (r.Result)
            {
                case "failure": return LaunchState.NoGo;
                case "success":
                    return Age(r, nowUtc) is TimeSpan a && a.TotalHours <= staleHours ? LaunchState.Go : LaunchState.AtRisk;
                case "skipped":
                case "cancelled":
                case "running":
                    return LaunchState.AtRisk;
                default:
                    return LaunchState.Unknown;
            }
        }

        /// <summary>
        /// A platform: the worst of its records (<paramref name="ids"/>) and the audit's verdict for it,
        /// with the one line that decided. A failed run always names itself first.
        /// </summary>
        public static LaunchState PlatformState(IEnumerable<string> ids, LaunchStatusBoard board,
            LaunchState audit, string auditWhy, DateTime nowUtc, double staleHours, out string why)
        {
            var records = new List<LaunchRecord>();
            if (ids != null && board != null)
                foreach (var id in ids)
                    if (board.Get(id) is { } r) records.Add(r);

            var state = LaunchState.Unknown;
            foreach (var r in records) state = Worst(state, StateOf(r, nowUtc, staleHours));

            foreach (var r in records)
                if (r.Result == "failure") { why = r.Id + " failed"; return LaunchState.NoGo; }

            if (audit != LaunchState.Unknown && Rank(audit) >= Rank(state))
            {
                why = string.IsNullOrEmpty(auditWhy) ? "audit: " + Word(audit) : auditWhy;
                return audit;
            }
            foreach (var r in records)
                if (r.Result == "success" && StateOf(r, nowUtc, staleHours) == LaunchState.AtRisk)
                {
                    why = r.Id + (Age(r, nowUtc) is TimeSpan a ? " is " + AgeText(a).Replace(" ago", " old") : " has no date");
                    return LaunchState.AtRisk;
                }
            foreach (var r in records)
                if (r.Result != "success" && StateOf(r, nowUtc, staleHours) == LaunchState.AtRisk)
                {
                    why = r.Id + " " + r.Result;
                    return LaunchState.AtRisk;
                }
            why = state == LaunchState.Go ? "every record green and fresh" : "no run recorded";
            return state;
        }

        /// <summary>"under an hour ago", "28 h ago", "24 days ago": the artifact's words.</summary>
        public static string AgeText(TimeSpan age)
        {
            var h = age.TotalHours;
            if (h < 1) return "under an hour ago";
            if (h < 48) return Math.Round(h).ToString(CultureInfo.InvariantCulture) + " h ago";
            return Math.Round(h / 24).ToString(CultureInfo.InvariantCulture) + " days ago";
        }

        /// <summary>Whole days from now to the milestone date (UTC midnight), rounded up.</summary>
        public static int DaysLeft(DateTime milestoneUtc, DateTime nowUtc) =>
            (int)Math.Ceiling((milestoneUtc - nowUtc).TotalDays);

        /// <summary>A plain https URL with no whitespace or markup characters, else null.</summary>
        public static string SafeUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.Ordinal) || url.Length <= 8)
                return null;
            foreach (var ch in url)
                if (char.IsWhiteSpace(ch) || ch == '"' || ch == '\'' || ch == '<' || ch == '>' || char.IsControl(ch))
                    return null;
            return url;
        }
    }
}

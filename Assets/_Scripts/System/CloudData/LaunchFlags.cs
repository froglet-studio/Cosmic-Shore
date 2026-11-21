using System;
using System.Collections.Generic;
using CosmicShore.Data;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The names of the remote kill switches, exactly as <c>Tools/UGS/CloudCode/launch-flags.js</c> writes them
    /// (its test reads this class and fails when the two lists differ). A new switch is added here, in the script's
    /// <c>KNOWN_KILL_SWITCHES</c> and in the gate it switches, in the same change.
    /// </summary>
    public static class LaunchKillSwitch
    {
        public const string Economy = "economy";
        public const string Scores = "scores";
        public const string MatchReports = "matchReports";
        public const string Trust = "trust";
        public const string Entitlements = "entitlements";
    }

    /// <summary>Where the flags in force came from.</summary>
    public enum LaunchFlagsSource
    {
        /// <summary>Nothing reached: the safe default (nothing killed, every version allowed).</summary>
        Default = 0,
        /// <summary>The last server answer, saved on this device and still fresh enough.</summary>
        Cache = 1,
        /// <summary>The server's answer this session.</summary>
        Live = 2,
    }

    /// <summary>
    /// One flags document as the <c>launch-flags</c> Cloud Code script returns it (Docs/LAUNCH/LIVE_FLAGS.md).
    /// Parsing is forgiving where it can be: an unknown kill switch or mode name (from a newer script) is ignored, a
    /// missing field takes its safe default. It refuses only what is not a flags document at all.
    /// </summary>
    public sealed class LaunchFlagsDocument
    {
        public const int Schema = 1;

        public string MinimumVersion { get; private set; } = string.Empty;
        public string RecommendedVersion { get; private set; } = string.Empty;
        public string UpdateUrlWindows { get; private set; } = string.Empty;
        public string UpdateUrlIos { get; private set; } = string.Empty;
        public string UpdateUrlAndroid { get; private set; } = string.Empty;
        public string MessageTitle { get; private set; } = string.Empty;
        public string MessageBody { get; private set; } = string.Empty;

        readonly HashSet<string> _killed = new(StringComparer.Ordinal);
        readonly HashSet<GameModes> _disabledModes = new();

        /// <summary>The safe default: nothing killed, every version allowed, no links, no message.</summary>
        public static LaunchFlagsDocument CreateDefault() => new();

        public bool IsKilled(string killSwitch) => !string.IsNullOrEmpty(killSwitch) && _killed.Contains(killSwitch);
        public bool IsModeDisabled(GameModes mode) => _disabledModes.Contains(mode);
        public IReadOnlyCollection<string> KilledSwitches => _killed;
        public IReadOnlyCollection<GameModes> DisabledModes => _disabledModes;

        /// <summary>The update link for a platform: Android, iPhone, else the Windows one.</summary>
        public string UpdateUrlFor(RuntimePlatform platform) => platform switch
        {
            RuntimePlatform.Android => UpdateUrlAndroid,
            RuntimePlatform.IPhonePlayer => UpdateUrlIos,
            _ => UpdateUrlWindows,
        };

        /// <summary>
        /// Reads a flags document. False (and <paramref name="doc"/> null) when <paramref name="json"/> is empty, not
        /// JSON, not an object, or carries a schema other than <see cref="Schema"/>.
        /// </summary>
        public static bool TryParse(string json, out LaunchFlagsDocument doc)
        {
            doc = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception) { return false; }

            var schema = root["schema"];
            if (schema == null || schema.Type != JTokenType.Integer || schema.Value<long>() != Schema) return false;

            var d = new LaunchFlagsDocument
            {
                MinimumVersion = Str(root["minimumVersion"]),
                RecommendedVersion = Str(root["recommendedVersion"]),
            };

            if (root["updateUrls"] is JObject urls)
            {
                d.UpdateUrlWindows = Str(urls["windows"]);
                d.UpdateUrlIos = Str(urls["ios"]);
                d.UpdateUrlAndroid = Str(urls["android"]);
            }

            if (root["killSwitches"] is JObject switches)
                foreach (var p in switches.Properties())
                    if (p.Value.Type == JTokenType.Boolean && p.Value.Value<bool>())
                        d._killed.Add(p.Name);

            if (root["disabledModes"] is JArray modes)
                foreach (var m in modes)
                    if (m.Type == JTokenType.String && TryParseMode(m.Value<string>(), out var mode))
                        d._disabledModes.Add(mode);

            if (root["message"] is JObject message)
            {
                d.MessageTitle = Str(message["title"]);
                d.MessageBody = Str(message["body"]);
            }

            doc = d;
            return true;
        }

        /// <summary>A GameModes member by its exact name; numbers are refused (an id is not a name).</summary>
        static bool TryParseMode(string name, out GameModes mode)
        {
            mode = default;
            if (string.IsNullOrEmpty(name) || !char.IsLetter(name[0])) return false;
            return Enum.TryParse(name, false, out mode) && Enum.IsDefined(typeof(GameModes), mode);
        }

        static string Str(JToken t) => t != null && t.Type == JTokenType.String ? t.Value<string>() ?? string.Empty : string.Empty;
    }

    /// <summary>
    /// The version comparison the gate uses: MAJOR.MINOR.PATCH, compared as numbers, with an optional pre-release
    /// (<c>-beta.2</c>) ordering below its release, as semver orders it. Forgiving on the client side: a missing part
    /// is 0 (<c>1.2</c> is <c>1.2.0</c>) and build metadata (<c>+abc</c>) is ignored. Anything it cannot read never
    /// locks a player out: <see cref="IsBelow"/> answers false.
    /// </summary>
    public static class LaunchVersion
    {
        public readonly struct Parsed
        {
            public readonly int Major, Minor, Patch;
            public readonly string PreRelease;

            public Parsed(int major, int minor, int patch, string preRelease)
            {
                Major = major; Minor = minor; Patch = patch; PreRelease = preRelease ?? string.Empty;
            }
        }

        public static bool TryParse(string version, out Parsed parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(version)) return false;
            string v = version.Trim();
            if (v.Length > 1 && (v[0] == 'v' || v[0] == 'V') && char.IsDigit(v[1])) v = v.Substring(1);

            int plus = v.IndexOf('+');
            if (plus >= 0) v = v.Substring(0, plus);
            string pre = string.Empty;
            int dash = v.IndexOf('-');
            if (dash >= 0)
            {
                pre = v.Substring(dash + 1);
                v = v.Substring(0, dash);
                if (pre.Length == 0) return false;
            }

            var parts = v.Split('.');
            if (parts.Length < 1 || parts.Length > 3) return false;
            var n = new int[3];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || parts[i].Length > 9) return false;
                foreach (char c in parts[i]) if (c < '0' || c > '9') return false;
                n[i] = int.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
            }
            parsed = new Parsed(n[0], n[1], n[2], pre);
            return true;
        }

        /// <summary>-1, 0 or 1 as semver orders <paramref name="a"/> and <paramref name="b"/>.</summary>
        public static int Compare(Parsed a, Parsed b)
        {
            if (a.Major != b.Major) return a.Major < b.Major ? -1 : 1;
            if (a.Minor != b.Minor) return a.Minor < b.Minor ? -1 : 1;
            if (a.Patch != b.Patch) return a.Patch < b.Patch ? -1 : 1;
            if (a.PreRelease == b.PreRelease) return 0;
            if (a.PreRelease.Length == 0) return 1;
            if (b.PreRelease.Length == 0) return -1;

            var p = a.PreRelease.Split('.');
            var q = b.PreRelease.Split('.');
            for (int i = 0; i < Math.Max(p.Length, q.Length); i++)
            {
                if (i >= p.Length) return -1;
                if (i >= q.Length) return 1;
                bool pn = int.TryParse(p[i], out int pi), qn = int.TryParse(q[i], out int qi);
                if (pn && qn) { if (pi != qi) return pi < qi ? -1 : 1; continue; }
                if (pn != qn) return pn ? -1 : 1;
                int c = string.CompareOrdinal(p[i], q[i]);
                if (c != 0) return c < 0 ? -1 : 1;
            }
            return 0;
        }

        /// <summary>
        /// True only when both versions read and <paramref name="current"/> is strictly below
        /// <paramref name="required"/>. An empty or unreadable value on either side is false: never a wall.
        /// </summary>
        public static bool IsBelow(string current, string required) =>
            TryParse(current, out var c) && TryParse(required, out var r) && Compare(c, r) < 0;
    }

    /// <summary>
    /// The live flags in force on this device (Docs/LAUNCH/LIVE_FLAGS.md): the remote kill switches for the server
    /// gates, the minimum and recommended versions, the disabled modes and an optional message, from the
    /// <c>launch-flags</c> Cloud Code script. <see cref="LaunchFlagsService"/> fetches it once after sign-in; before
    /// that (and when it cannot be reached) the last answer saved on this device is used while it is fresh enough,
    /// else the safe default.
    ///
    /// <para><b>A kill switch only ever turns a feature OFF.</b> Each gate reads
    /// <c>IsActive =&gt; LaunchFlags.Gate(Enabled, LaunchKillSwitch.X)</c>: the build's compiled gate AND not killed.
    /// A gate a build shipped off stays off whatever the server says.</para>
    /// </summary>
    public static class LaunchFlags
    {
        /// <summary>PlayerPrefs key of the last server answer (its raw JSON).</summary>
        public const string CacheJsonKey = "LaunchFlags.Json";
        /// <summary>PlayerPrefs key of when that answer arrived, as Unix seconds (a string: PlayerPrefs ints are 32-bit).</summary>
        public const string CacheTimeKey = "LaunchFlags.FetchedAtUnix";

        public static LaunchFlagsDocument Current { get; private set; } = LaunchFlagsDocument.CreateDefault();
        public static LaunchFlagsSource Source { get; private set; } = LaunchFlagsSource.Default;

        /// <summary>Raised on the main thread whenever <see cref="Current"/> is replaced.</summary>
        public static event Action OnChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = LaunchFlagsDocument.CreateDefault();
            Source = LaunchFlagsSource.Default;
            OnChanged = null;
        }

        /// <summary>
        /// Before the first scene: adopt the saved answer if it is fresh enough, so every gate read during boot (before
        /// sign-in) already honours a kill switch the last session heard about.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AdoptCacheAtBoot()
        {
            var (doc, source) = Resolve(null, ReadCachedJson(), ReadCachedAt(), NowUnix(),
                CosmicShore.ScriptableObjects.LaunchFlagsConfigSO.LoadOrDefault().CacheMaxAgeHours);
            Apply(doc, source);
        }

        /// <summary>
        /// The one composition rule for a server gate: on only if the build compiled it on AND the flags have not
        /// killed it. Never true when <paramref name="compiledOn"/> is false.
        /// </summary>
        public static bool Gate(bool compiledOn, string killSwitch) => compiledOn && !Current.IsKilled(killSwitch);

        public static bool IsKilled(string killSwitch) => Current.IsKilled(killSwitch);

        /// <summary>
        /// Whether the flags list this mode as disabled. Parsed and kept, but no menu reads it yet: the arcade grid has
        /// no availability path to hang it on (Docs/LAUNCH/LIVE_FLAGS.md, "disabledModes").
        /// </summary>
        public static bool IsModeDisabled(GameModes mode) => Current.IsModeDisabled(mode);

        /// <summary>
        /// Whether this build must update before it plays online. Only a server answer from THIS session can wall a
        /// player: a cached answer never does, so a player who is offline is never locked out of offline play.
        /// </summary>
        public static bool UpdateRequired => UpdateRequiredFor(Application.version, Current, Source);

        /// <summary>Whether a newer build is recommended (a dismissible notice). Live answers only, as above.</summary>
        public static bool UpdateRecommended => UpdateRecommendedFor(Application.version, Current, Source);

        public static bool UpdateRequiredFor(string currentVersion, LaunchFlagsDocument doc, LaunchFlagsSource source) =>
            source == LaunchFlagsSource.Live && doc != null && LaunchVersion.IsBelow(currentVersion, doc.MinimumVersion);

        public static bool UpdateRecommendedFor(string currentVersion, LaunchFlagsDocument doc, LaunchFlagsSource source) =>
            source == LaunchFlagsSource.Live && doc != null && !UpdateRequiredFor(currentVersion, doc, source)
            && LaunchVersion.IsBelow(currentVersion, doc.RecommendedVersion);

        /// <summary>
        /// The freshness rule, pure: a live answer that parses wins; else a cached answer that parses and is between 0
        /// and <paramref name="maxCacheAgeHours"/> old; else the safe default. A cache stamped in the future (a clock
        /// moved back) counts as stale.
        /// </summary>
        public static (LaunchFlagsDocument doc, LaunchFlagsSource source) Resolve(
            string liveJson, string cachedJson, long cachedAtUnix, long nowUnix, float maxCacheAgeHours)
        {
            if (LaunchFlagsDocument.TryParse(liveJson, out var live)) return (live, LaunchFlagsSource.Live);

            long age = nowUnix - cachedAtUnix;
            if (cachedAtUnix > 0 && age >= 0 && age <= (long)(Math.Max(0f, maxCacheAgeHours) * 3600f)
                && LaunchFlagsDocument.TryParse(cachedJson, out var cached))
                return (cached, LaunchFlagsSource.Cache);

            return (LaunchFlagsDocument.CreateDefault(), LaunchFlagsSource.Default);
        }

        /// <summary>Puts a document in force and tells listeners. Main thread.</summary>
        public static void Apply(LaunchFlagsDocument doc, LaunchFlagsSource source)
        {
            Current = doc ?? LaunchFlagsDocument.CreateDefault();
            Source = doc == null ? LaunchFlagsSource.Default : source;
            OnChanged?.Invoke();
        }

        /// <summary>Saves a server answer that parsed, with the time it arrived.</summary>
        public static void WriteCache(string json, long nowUnix)
        {
            PlayerPrefs.SetString(CacheJsonKey, json ?? string.Empty);
            PlayerPrefs.SetString(CacheTimeKey, nowUnix.ToString(System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        public static string ReadCachedJson() => PlayerPrefs.GetString(CacheJsonKey, string.Empty);

        public static long ReadCachedAt() =>
            long.TryParse(PlayerPrefs.GetString(CacheTimeKey, string.Empty), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out long t) ? t : 0L;

        public static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}

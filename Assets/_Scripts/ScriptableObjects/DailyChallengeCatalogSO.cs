using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The authored pool the daily challenge is drawn from, and the ONLY tuning surface for it -
    /// per Config Separation, nothing about a challenge's shape lives in code.
    ///
    /// <para>Authored through <b>FrogletTools &gt; Game Modes &gt; Daily Challenge</b>. The
    /// inspector works too, but the tool is what validates the two things that make a challenge
    /// unplayable and that no inspector can see: a target above the run's own end condition, and
    /// an intensity outside the mode's range.</para>
    ///
    /// <para><b>The draw is a pure function of the period</b> (<see cref="ForDate"/>): a
    /// platform-independent FNV-1a hash of the period key indexes the pool. So every client and
    /// every platform agrees on today's challenge with no server round trip, a cold launch can
    /// draw the card before Cloud Save answers, and an offline player still gets the right
    /// challenge. UGS stores the player's PROGRESS against it and nothing else.</para>
    ///
    /// <para>Loaded from <c>Resources/DailyChallengeCatalog</c>.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "DailyChallengeCatalog",
        menuName = "ScriptableObjects/" + nameof(DailyChallengeCatalogSO))]
    public class DailyChallengeCatalogSO : ScriptableObject
    {
        /// <summary>Resources path the runtime loads this from.</summary>
        public const string ResourcePath = "DailyChallengeCatalog";

        /// <summary>Attempts per period when <see cref="attemptsPerDay"/> is left at its default.</summary>
        public const int DefaultAttemptsPerDay = 1;

        /// <summary>
        /// One mode's challenge shape. The objective is DELIBERATELY fixed per mode rather than
        /// rolled per day: the day's variety is which MODE comes up, and a rolled target would
        /// give two players on the same date a different ask the moment their unlocked
        /// intensities differed.
        /// </summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("Off parks this entry without deleting it. NOTE: the draw indexes the ENABLED " +
                     "entries, so parking one re-rolls which date draws which mode.")]
            public bool Enabled = true;

            [Tooltip("The arcade mode this challenge plays. Must have a card in SO_GameList, or " +
                     "the draw produces a challenge nothing can launch.")]
            public GameModes Mode = GameModes.MultiplayerCrystalCapture;

            [Tooltip("Which per-player stat the objective counts. Normally the mode's own scoring " +
                     "metric - a challenge that counted something the mode does not surface would " +
                     "leave the player with no readout of their own progress.")]
            public ScoringMetric Metric = ScoringMetric.Crystals;

            [Tooltip("How much of Metric the LOCAL player must reach. Personal, never a domain sum.")]
            [Min(1)] public int Target = 15;

            [Tooltip("The mode's own race target for a DAILY run - this is what makes the daily " +
                     "version SMALLER than the real mode (Crystal Capture normally races to 20; a " +
                     "daily run can race to 8). 0 = use Target, which is almost always what you " +
                     "want: the objective and the run then end together.")]
            [Min(0)] public int EndConditionOverride;

            [Tooltip("Seconds from the turn starting. 0 = no time limit (the run's end condition " +
                     "then decides when the attempt is over).")]
            [Min(0)] public float TimeLimitSeconds = 60f;

            [Tooltip("Intensity the challenge is played at. Authored rather than rolled so the " +
                     "same date is the same ask for everyone; keep it inside the mode's own " +
                     "Min/MaxIntensity or it is silently clamped into range at launch.")]
            [Range(1, 4)] public int Intensity = 1;

            [Tooltip("The domain the player flies. Pinned like the intensity - the run seats the " +
                     "card's minimum, so the colour is not a team decision anyone else is party " +
                     "to. Jade is the default and is also what the menu resets every player to " +
                     "on spawn, so it is the one value that needs no request to take effect.")]
            public Domains Domain = Domains.Jade;

            [Tooltip("Verb + noun for the objective line, e.g. \"Collect\" / \"crystals\". Shown as " +
                     "\"Collect 15 crystals in 1:00\".")]
            public string Verb = "Collect";
            public string Noun = "crystals";

            /// <summary>
            /// The race target a daily run of this entry actually uses: the override when
            /// authored, otherwise the objective itself.
            /// </summary>
            public int ResolvedEndCondition => EndConditionOverride > 0 ? EndConditionOverride : Target;
        }

        /// <summary>
        /// Editor / development-build shortcuts for testing the cycle without waiting a day.
        /// <b>Every one of these is inert in a release player</b> (see <see cref="TestActive"/>),
        /// and <c>DailyChallengeTestModeBuildGuard</c> fails a non-development build outright
        /// while <see cref="enabled"/> is left on - a read-time gate alone would ship a build
        /// whose behaviour depends on a flag nobody meant to leave set.
        /// </summary>
        [Serializable]
        public class TestSettings
        {
            [Tooltip("Master switch. Everything below is ignored while this is off, and in any " +
                     "release build regardless.")]
            public bool enabled;

            [Tooltip("Pin the draw to one pool entry by index instead of hashing the date. " +
                     "-1 = draw normally.")]
            public int forcedPoolIndex = -1;

            [Tooltip("Shrink the cycle so rollover is testable: a 'day' becomes this many real " +
                     "minutes. 0 = the real 24h UTC day. The period key changes shape (T<n>), so " +
                     "a test period can never be mistaken for a real date - switching back wipes " +
                     "the stored progress, which is the honest outcome.")]
            [Min(0)] public float dayLengthMinutes;

            [Tooltip("Ignore the once-per-day attempt limit, so a challenge can be replayed while " +
                     "tuning it.")]
            public bool ignoreAttemptLimit;

            [Tooltip("Multiplies every entry's time limit. 0.25 turns a 60s challenge into 15s. " +
                     "1 = as authored.")]
            [Min(0.01f)] public float timeLimitScale = 1f;
        }

        [Tooltip("Today's challenge is drawn from this pool by a hash of the period key. Order is " +
                 "part of the draw, so REORDERING re-rolls which day gets which mode - append " +
                 "rather than insert if that matters.")]
        public List<Entry> Pool = new();

        [Tooltip("How many attempts a player gets per period. 1 (the default) is the design: the " +
                 "daily challenge is played ONCE - the attempt is spent at launch, so quitting " +
                 "mid-run does not buy a retry. 0 = unlimited.")]
        [Min(0)] public int attemptsPerDay = DefaultAttemptsPerDay;

        [Tooltip("When on, a mode the player has not unlocked through the quest chain is skipped " +
                 "by the draw. OFF by design: the daily challenge is a curated invitation into a " +
                 "mode you may not have reached yet, and skipping per player would mean two " +
                 "players no longer share a date's challenge.")]
        public bool respectModeProgression = false;

        [Header("Testing (never ships)")]
        public TestSettings test = new();

        static DailyChallengeCatalogSO _instance;

        /// <summary>Cached runtime accessor. Null only when the asset is missing.</summary>
        public static DailyChallengeCatalogSO Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<DailyChallengeCatalogSO>(ResourcePath);
                return _instance;
            }
        }

        /// <summary>
        /// True when the test shortcuts are BOTH switched on and legal to apply. A release player
        /// answers false whatever the asset says, so a flag left on cannot change shipped
        /// behaviour even if the build guard were bypassed.
        /// </summary>
        public bool TestActive => test != null && test.enabled && (Application.isEditor || Debug.isDebugBuild);

        /// <summary>Attempts per period, honouring the test override. 0 = unlimited.</summary>
        public int EffectiveAttemptsPerDay =>
            TestActive && test.ignoreAttemptLimit ? 0 : Mathf.Max(0, attemptsPerDay);

        // ── Periods ────────────────────────────────────────────────────────────

        /// <summary>The "yyyy-MM-dd" key a UTC instant belongs to. The one date formatter.</summary>
        public static string DateKeyFor(DateTime utc) =>
            utc.ToUniversalTime().Date.ToString("yyyy-MM-dd");

        /// <summary>
        /// UTC midnight after <paramref name="utc"/> - when the current challenge is replaced.
        /// </summary>
        public static DateTime NextRolloverUtc(DateTime utc) =>
            utc.ToUniversalTime().Date.AddDays(1);

        /// <summary>
        /// The key of the period an instant falls in - the real UTC day normally, or a shortened
        /// test period. Deliberately a DIFFERENT SHAPE ("T42") from a real date key, so a record
        /// written under a shrunken cycle can never be read as a real day's progress.
        /// </summary>
        public string PeriodKeyFor(DateTime utc)
        {
            if (!TestActive || test.dayLengthMinutes <= 0f)
                return DateKeyFor(utc);

            return "T" + PeriodIndex(utc, test.dayLengthMinutes).ToString();
        }

        /// <summary>When the current period ends. The card counts down to this.</summary>
        public DateTime PeriodEndUtc(DateTime utc)
        {
            if (!TestActive || test.dayLengthMinutes <= 0f)
                return NextRolloverUtc(utc);

            long index = PeriodIndex(utc, test.dayLengthMinutes);
            return Epoch.AddMinutes((index + 1) * (double)test.dayLengthMinutes);
        }

        // Written out rather than DateTime.UnixEpoch: that member only exists from .NET Standard
        // 2.1, and the API level is a project setting this file has no business depending on.
        static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static long PeriodIndex(DateTime utc, float lengthMinutes)
        {
            double minutes = (utc.ToUniversalTime() - Epoch).TotalMinutes;
            return (long)Math.Floor(minutes / lengthMinutes);
        }

        // ── The draw ───────────────────────────────────────────────────────────

        /// <summary>
        /// Platform-independent FNV-1a over the period key. <see cref="System.Random"/> is
        /// deterministic only within one runtime's implementation, which is not a promise two
        /// clients on two platforms can hold each other to - and "everyone gets the same daily
        /// challenge" is the whole feature.
        /// </summary>
        public static uint HashDateKey(string dateKey)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < dateKey.Length; i++)
                {
                    hash ^= dateKey[i];
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        /// <summary>
        /// The challenge for the period an instant falls in. Returns an invalid (default)
        /// challenge when the pool is empty or every entry was filtered out - callers must check
        /// <see cref="DailyChallenge.IsValid"/> rather than assume one exists.
        /// </summary>
        /// <param name="utc">Any instant in the period in question.</param>
        /// <param name="isModeAvailable">
        /// Optional filter (mode → playable). Applied only when
        /// <see cref="respectModeProgression"/> is on.
        /// </param>
        public DailyChallenge ForDate(DateTime utc, Func<GameModes, bool> isModeAvailable = null)
        {
            string periodKey = PeriodKeyFor(utc);

            var candidates = new List<Entry>(Pool != null ? Pool.Count : 0);
            if (Pool != null)
            {
                for (int i = 0; i < Pool.Count; i++)
                {
                    var e = Pool[i];
                    if (e == null || !e.Enabled || e.Target <= 0) continue;
                    if (respectModeProgression && isModeAvailable != null && !isModeAvailable(e.Mode)) continue;
                    candidates.Add(e);
                }
            }

            if (candidates.Count == 0)
                return default;

            // The forced index addresses the AUTHORED pool, not the filtered candidate list -
            // "entry 3 in the tool" has to mean the row the author is looking at.
            Entry entry = null;
            if (TestActive && test.forcedPoolIndex >= 0 && Pool != null &&
                test.forcedPoolIndex < Pool.Count)
            {
                var forced = Pool[test.forcedPoolIndex];
                if (forced != null && forced.Target > 0) entry = forced;
            }

            entry ??= candidates[(int)(HashDateKey(periodKey) % (uint)candidates.Count)];

            float timeLimit = Mathf.Max(0f, entry.TimeLimitSeconds);
            if (TestActive && test.timeLimitScale > 0f && timeLimit > 0f)
                timeLimit *= test.timeLimitScale;

            return new DailyChallenge
            {
                DateKey          = periodKey,
                GameMode         = entry.Mode,
                Intensity        = Mathf.Clamp(entry.Intensity, 1, 4),
                Domain           = ResolvePlayableDomain(entry.Domain),
                Metric           = entry.Metric,
                TargetValue      = entry.Target,
                EndConditionValue= entry.ResolvedEndCondition,
                TimeLimitSeconds = timeLimit,
                ObjectiveText    = BuildObjectiveText(entry, timeLimit),
            };
        }

        /// <summary>
        /// The domain a challenge is actually flown on. Anything outside the PLAYABLE set falls
        /// back to Jade - Blue is the "no team" sentinel and is never a colour a player flies, and
        /// <c>Domains</c> has no member at 0, which is exactly what an entry authored before the
        /// field existed deserializes to.
        /// </summary>
        public static Domains ResolvePlayableDomain(Domains domain) => domain switch
        {
            Domains.Jade => Domains.Jade,
            Domains.Ruby => Domains.Ruby,
            Domains.Gold => Domains.Gold,
            _            => Domains.Jade,
        };

        /// <summary>"Collect 15 crystals in 1:00" - the ONE composition of the objective line, so
        /// the card, the launch panel and the in-game readout can never word it differently.</summary>
        public static string BuildObjectiveText(Entry entry) =>
            BuildObjectiveText(entry, entry.TimeLimitSeconds);

        /// <summary>
        /// As above with an explicit time budget, so a test-scaled clock is described honestly
        /// rather than by the authored number the run is not using.
        /// </summary>
        public static string BuildObjectiveText(Entry entry, float timeLimitSeconds)
        {
            string verb = string.IsNullOrWhiteSpace(entry.Verb) ? "Score" : entry.Verb.Trim();
            string noun = string.IsNullOrWhiteSpace(entry.Noun) ? "points" : entry.Noun.Trim();
            string body = $"{verb} {entry.Target} {noun}";

            return timeLimitSeconds > 0f
                ? $"{body} in {FormatDuration(timeLimitSeconds)}"
                : body;
        }

        /// <summary>m:ss for a time budget.</summary>
        public static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60}:{total % 60:D2}";
        }
    }
}

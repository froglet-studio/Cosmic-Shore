using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The authored pool the weekly challenge is drawn from, and the ONLY tuning surface for it -
    /// per Config Separation, nothing about a challenge's shape lives in code.
    ///
    /// <para>Authored through <b>FrogletTools &gt; Game Modes &gt; Weekly Challenge</b>. The
    /// inspector works too, but the tool is what validates the two things that make a challenge
    /// unplayable and that no inspector can see: a target above the run's own end condition, and
    /// an intensity outside the mode's range.</para>
    ///
    /// <para><b>The draw is a pure function of the period</b> (<see cref="ForDate"/>): a
    /// platform-independent FNV-1a hash of the period key indexes the pool. So every client and
    /// every platform agrees on this week's challenge with no server round trip, a cold launch can
    /// draw the card before Cloud Save answers, and an offline player still gets the right
    /// challenge. UGS stores the player's PROGRESS against it and nothing else.</para>
    ///
    /// <para>Loaded from <c>Resources/WeeklyChallengeCatalog</c>.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "WeeklyChallengeCatalog",
        menuName = "ScriptableObjects/" + nameof(WeeklyChallengeCatalogSO))]
    public class WeeklyChallengeCatalogSO : ScriptableObject
    {
        /// <summary>Resources path the runtime loads this from.</summary>
        public const string ResourcePath = "WeeklyChallengeCatalog";

        /// <summary>Attempts per period when <see cref="attemptsPerPeriod"/> is left at its default.</summary>
        public const int DefaultAttemptsPerPeriod = 1;

        /// <summary>
        /// One mode's challenge shape. The objective is DELIBERATELY fixed per mode rather than
        /// rolled per week: the week's variety is which MODE comes up, and a rolled target would
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
            public GameModes Mode = GameModes.Scurry;

            [Tooltip("Which per-player stat the objective counts. Normally the mode's own scoring " +
                     "metric - a challenge that counted something the mode does not surface would " +
                     "leave the player with no readout of their own progress.")]
            public ScoringMetric Metric = ScoringMetric.Crystals;

            [Tooltip("How much of Metric the LOCAL player must reach. Personal, never a domain sum.")]
            [Min(1)] public int Target = 15;

            

            [Tooltip("Intensity the challenge is played at, PINNED - the row offers only this " +
                     "one. Authored rather than rolled so the same week is the same ask for " +
                     "everyone; keep it inside the mode's own Min/MaxIntensity or it is silently " +
                     "clamped into range at launch.")]
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
        }

        /// <summary>
        /// Editor / development-build shortcuts for testing the cycle without waiting a day.
        /// <b>Every one of these is inert in a release player</b> (see <see cref="TestActive"/>),
        /// and <c>WeeklyChallengeTestModeBuildGuard</c> fails a non-development build outright
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
                     "minutes. 0 = the real UTC week. The period key changes shape (T<n>), so " +
                     "a test period can never be mistaken for a real date - switching back wipes " +
                     "the stored progress, which is the honest outcome.")]
            [Min(0)] public float periodLengthMinutes;

            [Tooltip("Ignore the once-per-day attempt limit, so a challenge can be replayed while " +
                     "tuning it.")]
            public bool ignoreAttemptLimit;
        }

        [Tooltip("ThisWeek's challenge is drawn from this pool by a hash of the period key. Order is " +
                 "part of the draw, so REORDERING re-rolls which day gets which mode - append " +
                 "rather than insert if that matters.")]
        public List<Entry> Pool = new();

        [Tooltip("How many attempts a player gets per period. 1 (the default) is the design: the " +
                 "weekly challenge is played ONCE - the attempt is spent at launch, so quitting " +
                 "mid-run does not buy a retry. 0 = unlimited.")]
        [Min(0)] public int attemptsPerPeriod = DefaultAttemptsPerPeriod;

        [Tooltip("UGS Leaderboards id for the weekly ranking. Empty = ranking off (the challenge " +
                 "itself is unaffected). ONE id for every week - the board is reset weekly by UGS, " +
                 "not minted per week, because the SDK cannot create leaderboards. Its Sort Order " +
                 "must be ASCENDING (the score is a TIME), its update strategy KEEP BEST, and its " +
                 "reset schedule must ARCHIVE - the archive is the only record of who won a week " +
                 "once the board has rolled over.")]
        public string leaderboardId = "";

        [Tooltip("RE-ISSUE this week's challenge to everyone. Bump it by one and every player's " +
                 "stored progress for the current period is treated as belonging to an earlier " +
                 "one, so their ATTEMPT comes back - and their best value and completion flag are " +
                 "cleared with it, because the record and the attempt are one record.\n\n" +
                 "It does NOT change which mode the week draws. Leave it alone unless a bug ate " +
                 "people's attempts; it is a remedy, not a tuning value.")]
        [Min(0)] public int attemptResetToken;

        /// <summary>
        /// One regional board. See <see cref="CosmicShore.Core.WeeklyChallengeRegion"/> for why a
        /// region has to be its OWN board rather than a filter over the world one.
        /// </summary>
        [Serializable]
        public class RegionalBoard
        {
            [Tooltip("Region key, matched case-insensitively against the player's resolved region. " +
                     "The device answer is a two-letter ISO country (us, gb, sg), so list every " +
                     "country a board covers - one row per country, several rows may share an id.")]
            public string regionKey = "";

            [Tooltip("UGS Leaderboards id for this region. Create it in the dashboard with the " +
                     "SAME settings as the world board: Sort Order ASCENDING, update strategy " +
                     "KEEP BEST, weekly reset with archiving ON. Empty parks the row.")]
            public string leaderboardId = "";
        }

        [Tooltip("Per-region boards for the Regional tab. EMPTY is a supported state and the " +
                 "default: the tab reports that no regional board is configured rather than " +
                 "showing the world board under a regional heading. A player whose region matches " +
                 "no row submits to the world board only.")]
        public List<RegionalBoard> regionalLeaderboards = new();

        /// <summary>
        /// The board id for a region key, or null when that region has none. Case-insensitive, and
        /// the FIRST matching row wins so a duplicated key is a no-op rather than an error.
        /// </summary>
        public string RegionalLeaderboardId(string regionKey)
        {
            if (string.IsNullOrWhiteSpace(regionKey) || regionalLeaderboards == null) return null;

            foreach (var board in regionalLeaderboards)
            {
                if (board == null) continue;
                if (string.IsNullOrWhiteSpace(board.leaderboardId)) continue;
                if (string.Equals(board.regionKey, regionKey, StringComparison.OrdinalIgnoreCase))
                    return board.leaderboardId;
            }
            return null;
        }

        [Tooltip("When on, a mode the player has not unlocked through the quest chain is skipped " +
                 "by the draw. OFF by design: the weekly challenge is a curated invitation into a " +
                 "mode you may not have reached yet, and skipping per player would mean two " +
                 "players no longer share a date's challenge.")]
        public bool respectModeProgression = false;

        [Header("Testing (never ships)")]
        public TestSettings test = new();

        static WeeklyChallengeCatalogSO _instance;

        /// <summary>Cached runtime accessor. Null only when the asset is missing.</summary>
        public static WeeklyChallengeCatalogSO Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<WeeklyChallengeCatalogSO>(ResourcePath);
                return _instance;
            }
        }

        /// <summary>
        /// True when the test shortcuts are BOTH switched on and legal to apply. A release player
        /// answers false whatever the asset says, so a flag left on cannot change shipped
        /// behaviour even if the build guard were bypassed.
        /// </summary>
        public bool TestActive => test != null && test.enabled && (Application.isEditor || Debug.isDebugBuild);

        /// <summary>Attempts per week, honouring the test override. 0 = unlimited.</summary>
        public int EffectiveAttemptsPerPeriod =>
            TestActive && test.ignoreAttemptLimit ? 0 : Mathf.Max(0, attemptsPerPeriod);

        // ── Periods ────────────────────────────────────────────────────────────

        /// <summary>
        /// The UTC MONDAY that starts the week an instant belongs to. Everything else about a week
        /// is derived from this one function.
        ///
        /// <para>Monday because ISO-8601 says so, and a week boundary is exactly the kind of thing
        /// that must not be a matter of taste - a client that started weeks on Sunday would draw a
        /// different challenge from its neighbour for one day in seven, and only for players in
        /// that day. UTC for the same reason the day cycle used it: a local-time boundary makes the
        /// challenge change at a different moment for every timezone.</para>
        /// </summary>
        public static DateTime WeekStartUtc(DateTime utc)
        {
            var date = utc.ToUniversalTime().Date;
            // DayOfWeek numbers Sunday = 0; ISO wants Monday = 0, so shift Sunday to the END.
            int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-daysSinceMonday);
        }

        /// <summary>The "yyyy-MM-dd" key of the WEEK a UTC instant belongs to (its Monday). The one
        /// period formatter — every record, draw and countdown keys off this.</summary>
        public static string WeekKeyFor(DateTime utc) =>
            WeekStartUtc(utc).ToString("yyyy-MM-dd");

        /// <summary>
        /// The UTC Monday midnight after <paramref name="utc"/> — when the current challenge is
        /// replaced and the leaderboard's week closes.
        /// </summary>
        public static DateTime NextRolloverUtc(DateTime utc) =>
            WeekStartUtc(utc).AddDays(7);

        /// <summary>
        /// The key of the period an instant falls in — the real UTC week normally, or a shortened
        /// test period. Deliberately a DIFFERENT SHAPE ("T42") from a real week key, so a record
        /// written under a shrunken cycle can never be read as a real week's progress.
        /// </summary>
        public string PeriodKeyFor(DateTime utc)
        {
            if (!TestActive || test.periodLengthMinutes <= 0f)
                return WeekKeyFor(utc);

            return "T" + PeriodIndex(utc, test.periodLengthMinutes).ToString();
        }

        /// <summary>
        /// The key a player's PROGRESS is filed under. Normally identical to
        /// <see cref="PeriodKeyFor"/>; with <see cref="attemptResetToken"/> raised it carries the
        /// token (<c>2026-09-01#2</c>), which makes every record written before the bump read as
        /// STALE and be reset - attempts included.
        ///
        /// <para><b>Separate from the draw key on purpose.</b> The mode is chosen by hashing the
        /// period key, so folding the token into that key would silently change which game this
        /// week is - a reset would look like a re-roll, and a player mid-week would find the
        /// challenge had become a different one. Re-issuing the SAME challenge is the whole
        /// point.</para>
        ///
        /// <para>It reuses the staleness path that already exists for a week rollover
        /// (<c>WeeklyChallengeCloudData.IsStale</c>) rather than adding a second way to clear a
        /// record: a remedy with its own code path is a remedy nobody has tested.</para>
        /// </summary>
        public string RecordKeyFor(DateTime utc)
        {
            string period = PeriodKeyFor(utc);
            return attemptResetToken > 0 ? period + "#" + attemptResetToken : period;
        }

        /// <summary>When the current period ends. The card counts down to this.</summary>
        public DateTime PeriodEndUtc(DateTime utc)
        {
            if (!TestActive || test.periodLengthMinutes <= 0f)
                return NextRolloverUtc(utc);

            long index = PeriodIndex(utc, test.periodLengthMinutes);
            return Epoch.AddMinutes((index + 1) * (double)test.periodLengthMinutes);
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
        /// clients on two platforms can hold each other to - and "everyone gets the same weekly
        /// challenge" is the whole feature.
        /// </summary>
        public static uint HashPeriodKey(string periodKey)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < periodKey.Length; i++)
                {
                    hash ^= periodKey[i];
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        /// <summary>
        /// The challenge for the period an instant falls in. Returns an invalid (default)
        /// challenge when the pool is empty or every entry was filtered out - callers must check
        /// <see cref="WeeklyChallenge.IsValid"/> rather than assume one exists.
        /// </summary>
        /// <param name="utc">Any instant in the period in question.</param>
        /// <param name="isModeAvailable">
        /// Optional filter (mode → playable). Applied only when
        /// <see cref="respectModeProgression"/> is on.
        /// </param>
        public WeeklyChallenge ForDate(DateTime utc, Func<GameModes, bool> isModeAvailable = null)
        {
            // The DRAW is keyed on the period alone, so a reset token re-issues this week's
            // challenge rather than re-rolling it into a different mode.
            string drawKey = PeriodKeyFor(utc);
            string periodKey = RecordKeyFor(utc);

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

            entry ??= candidates[(int)(HashPeriodKey(drawKey) % (uint)candidates.Count)];

            return new WeeklyChallenge
            {
                PeriodKey          = periodKey,
                GameMode         = entry.Mode,
                Intensity        = Mathf.Clamp(entry.Intensity, 1, 4),
                Domain           = ResolvePlayableDomain(entry.Domain),
                Metric           = entry.Metric,
                TargetValue      = entry.Target,
                ObjectiveText    = BuildObjectiveText(entry),
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
        /// the card, the launch panel and the in-game readout can never word it differently.
        ///
        /// <para>There is NO duration in it any more. A weekly challenge is an ordinary match of
        /// its mode played for a personal objective on top, so "in 1:30" described a rule the run
        /// no longer has - see <c>Docs/WEEKLY_CHALLENGE.md</c>.</para>
        /// </summary>
        public static string BuildObjectiveText(Entry entry) =>
            entry == null
                ? ""
                : $"{(string.IsNullOrWhiteSpace(entry.Verb) ? "Reach" : entry.Verb.Trim())} " +
                  $"{Mathf.Max(1, entry.Target)} " +
                  $"{(string.IsNullOrWhiteSpace(entry.Noun) ? "points" : entry.Noun.Trim())}";
    }
}

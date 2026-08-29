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
    /// <para><b>The draw is a pure function of the UTC date</b> (<see cref="ForDate"/>): a
    /// platform-independent FNV-1a hash of "yyyy-MM-dd" indexes the pool. So every client and
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

        /// <summary>
        /// One mode's challenge shape. The objective is DELIBERATELY fixed per mode rather than
        /// rolled per day: the day's variety is which MODE comes up, and a rolled target would
        /// give two players on the same date a different ask the moment their unlocked
        /// intensities differed.
        /// </summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("The arcade mode this challenge plays. Must have a live scene and a card in " +
                     "SO_GameList, or the draw skips it.")]
            public GameModes Mode = GameModes.MultiplayerCrystalCapture;

            [Tooltip("Which per-player stat the objective counts. Normally the mode's own scoring " +
                     "metric - a challenge that counted something the mode does not surface would " +
                     "leave the player with no readout of their own progress.")]
            public ScoringMetric Metric = ScoringMetric.Crystals;

            [Tooltip("How much of Metric the LOCAL player must reach. Personal, never a domain sum.")]
            [Min(1)] public int Target = 30;

            [Tooltip("Seconds from the turn starting. 0 = no time limit (the mode's own end " +
                     "condition then decides when the attempt is over).")]
            [Min(0)] public float TimeLimitSeconds = 60f;

            [Tooltip("Intensity the challenge is played at. Authored rather than rolled so the " +
                     "same date is the same ask for everyone; keep it inside the mode's own " +
                     "Min/MaxIntensity.")]
            [Range(1, 4)] public int Intensity = 1;

            [Tooltip("Verb + noun for the objective line, e.g. \"Collect\" / \"crystals\". Shown as " +
                     "\"Collect 30 crystals in 1:00\".")]
            public string Verb = "Collect";
            public string Noun = "crystals";
        }

        [Tooltip("Today's challenge is drawn from this pool by a hash of the UTC date. Order is " +
                 "part of the draw, so REORDERING re-rolls which day gets which mode - append " +
                 "rather than insert if that matters.")]
        public List<Entry> Pool = new();

        [Tooltip("When on, a mode the player has not unlocked through the quest chain is skipped " +
                 "by the draw. OFF by design: the daily challenge is a curated invitation into a " +
                 "mode you may not have reached yet, and skipping per player would mean two " +
                 "players no longer share a date's challenge.")]
        public bool respectModeProgression = false;

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

        /// <summary>The "yyyy-MM-dd" key a UTC instant belongs to. The one date formatter.</summary>
        public static string DateKeyFor(DateTime utc) =>
            utc.ToUniversalTime().Date.ToString("yyyy-MM-dd");

        /// <summary>
        /// UTC midnight after <paramref name="utc"/> - when the current challenge is replaced.
        /// </summary>
        public static DateTime NextRolloverUtc(DateTime utc) =>
            utc.ToUniversalTime().Date.AddDays(1);

        /// <summary>
        /// Platform-independent FNV-1a over the date key. <see cref="System.Random"/> is
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
        /// The challenge for a UTC day. Returns an invalid (default) challenge when the pool is
        /// empty or every entry was filtered out - callers must check
        /// <see cref="DailyChallenge.IsValid"/> rather than assume one exists.
        /// </summary>
        /// <param name="utc">Any instant on the day in question.</param>
        /// <param name="isModeAvailable">
        /// Optional filter (mode → playable). Applied only when
        /// <see cref="respectModeProgression"/> is on.
        /// </param>
        public DailyChallenge ForDate(DateTime utc, Func<GameModes, bool> isModeAvailable = null)
        {
            string dateKey = DateKeyFor(utc);

            var candidates = new List<Entry>(Pool != null ? Pool.Count : 0);
            if (Pool != null)
            {
                for (int i = 0; i < Pool.Count; i++)
                {
                    var e = Pool[i];
                    if (e == null || e.Target <= 0) continue;
                    if (respectModeProgression && isModeAvailable != null && !isModeAvailable(e.Mode)) continue;
                    candidates.Add(e);
                }
            }

            if (candidates.Count == 0)
                return default;

            var entry = candidates[(int)(HashDateKey(dateKey) % (uint)candidates.Count)];

            return new DailyChallenge
            {
                DateKey          = dateKey,
                GameMode         = entry.Mode,
                Intensity        = Mathf.Clamp(entry.Intensity, 1, 4),
                Metric           = entry.Metric,
                TargetValue      = entry.Target,
                TimeLimitSeconds = Mathf.Max(0f, entry.TimeLimitSeconds),
                ObjectiveText    = BuildObjectiveText(entry),
            };
        }

        /// <summary>"Collect 30 crystals in 1:00" - the ONE composition of the objective line, so
        /// the card, the launch panel and the in-game readout can never word it differently.</summary>
        public static string BuildObjectiveText(Entry entry)
        {
            string verb = string.IsNullOrWhiteSpace(entry.Verb) ? "Score" : entry.Verb.Trim();
            string noun = string.IsNullOrWhiteSpace(entry.Noun) ? "points" : entry.Noun.Trim();
            string body = $"{verb} {entry.Target} {noun}";

            return entry.TimeLimitSeconds > 0f
                ? $"{body} in {FormatDuration(entry.TimeLimitSeconds)}"
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

using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// All Fake Artist mode tuning in one shared asset (CLAUDE.md config-separation:
    /// timings, point values, and AI behavior live in data, never hardcoded). The WIN
    /// target (first to N points, default 8) deliberately does NOT live here - end-game
    /// counts are authored only via Tools &gt; Cosmic Shore &gt; End Game Conditions
    /// (EndConditionOverridesSO.fakeArtistWinTarget).
    ///
    /// Several knobs scale with the round's player count (3-12) via the helpers at the
    /// bottom: strokes-per-player (so the total stays recognizable in small groups), the
    /// number of fake artists, and the fake-artist reward.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Game/FakeArtistConfig", fileName = "FakeArtistConfig")]
    public class FakeArtistConfigSO : ScriptableObject
    {
        [Header("Artwork")]
        [Tooltip("Local size of each round's artwork (preset generation scale).")]
        [Min(100f)] public float ArtworkSize = 600f;

        [Tooltip("Ride-dot reach: latch radius scale for guide rings (ring radius = max(18, reach*1.8)).")]
        [Min(4f)] public float DotReach = 24f;

        [Header("Strokes / players")]
        [Tooltip("Minimum strokes each player draws per round (also the value used at high player counts).")]
        [Range(1, 6)] public int StrokesPerPlayer = 3;

        [Tooltip("Maximum strokes each player draws per round (small groups draw more each so the picture reads).")]
        [Range(1, 8)] public int MaxStrokesPerPlayer = 6;

        [Tooltip("Target minimum TOTAL strokes in the artwork - enough that players can still tell what it is. " +
                 "Strokes-per-player is raised (up to Max) in small groups to hit this.")]
        [Range(6, 48)] public int MinTotalStrokes = 15;

        [Header("Fake artists")]
        [Tooltip("At or above this player count the round has TWO fake artists instead of one.")]
        [Range(3, 12)] public int SecondImposterAtPlayers = 8;

        [Header("Round timing (seconds)")]
        [Tooltip("Drawing phase time limit. The phase also ends early once every player finishes their strokes.")]
        [Min(10f)] public float DrawSeconds = 150f;

        [Tooltip("Voting phase time limit. Also ends early once every eligible human has voted.")]
        [Min(5f)] public float VoteSeconds = 30f;

        [Tooltip("Reveal display time before the next round's ready screen.")]
        [Min(2f)] public float RevealSeconds = 8f;

        [Header("Points")]
        [Tooltip("Awarded to a voter who guessed the artwork's subject.")]
        public int CorrectSubjectPoints = 1;

        [Tooltip("Awarded to a voter who guessed (any) fake artist.")]
        public int CorrectImposterPoints = 1;

        [Tooltip("Applied (once) to any player at least one voter accused of being the fake artist. Negative.")]
        public int GuessedPenalty = -1;

        [Tooltip("Base points awarded to each fake artist every round, caught or not.")]
        public int ImposterReward = 4;

        [Tooltip("Fake-artist reward gains +1 for every this-many players above the minimum (larger galleries " +
                 "catch a fake more easily, so the payoff scales up).")]
        [Range(1, 12)] public int ImposterRewardPlayersPerBonus = 4;

        [Tooltip("Upper clamp on the scaled fake-artist reward.")]
        public int ImposterRewardMax = 7;

        [Header("Vote")]
        [Tooltip("Number of subject options shown (correct + decoys).")]
        [Range(2, 8)] public int SubjectChoiceCount = 4;

        [Tooltip("During voting, frame each player's camera on the shared painting so they study it while answering.")]
        public bool GalleryCameraDuringVote = true;

        [Header("AI voters")]
        [Tooltip("Chance an AI voter picks the correct subject (otherwise a random decoy).")]
        [Range(0f, 1f)] public float AICorrectSubjectChance = 0.35f;

        [Tooltip("Chance an AI voter accuses an actual fake artist (otherwise a random other player).")]
        [Range(0f, 1f)] public float AICorrectImposterChance = 0.3f;

        // ── Player-count scaling ─────────────────────────────────────────────

        /// <summary>
        /// Strokes each player draws this round. Small groups draw more each so the total
        /// stays above <see cref="MinTotalStrokes"/> (a recognizable picture); large groups
        /// settle at <see cref="StrokesPerPlayer"/>.
        /// </summary>
        public int StrokesPerPlayerFor(int playerCount)
        {
            if (playerCount < 1) playerCount = 1;
            int needed = Mathf.CeilToInt((float)MinTotalStrokes / playerCount);
            return Mathf.Clamp(needed, StrokesPerPlayer, Mathf.Max(StrokesPerPlayer, MaxStrokesPerPlayer));
        }

        /// <summary>Number of fake artists this round (1, or 2 in large groups).</summary>
        public int ImposterCountFor(int playerCount) =>
            playerCount >= SecondImposterAtPlayers ? 2 : 1;

        /// <summary>
        /// Per-fake-artist reward this round, scaled up with player count (a fake blends in
        /// among more players but also faces more accusers).
        /// </summary>
        public int ImposterRewardFor(int playerCount, int minPlayers)
        {
            int perBonus = Mathf.Max(1, ImposterRewardPlayersPerBonus);
            int bonus = Mathf.Max(0, playerCount - minPlayers) / perBonus;
            return Mathf.Clamp(ImposterReward + bonus, ImposterReward, Mathf.Max(ImposterReward, ImposterRewardMax));
        }
    }
}

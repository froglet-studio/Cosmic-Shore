using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// All Fake Artist mode tuning in one shared asset (CLAUDE.md config-separation:
    /// timings, point values, and AI behavior live in data, never hardcoded). The WIN
    /// target (first to N points, default 8) deliberately does NOT live here - end-game
    /// counts are authored only via Tools &gt; Cosmic Shore &gt; End Game Conditions
    /// (EndConditionOverridesSO.fakeArtistWinTarget).
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Game/FakeArtistConfig", fileName = "FakeArtistConfig")]
    public class FakeArtistConfigSO : ScriptableObject
    {
        [Header("Artwork")]
        [Tooltip("Local size of each round's artwork (preset generation scale). 'Medium' strokes fall out of size / (players*3).")]
        [Min(100f)] public float ArtworkSize = 600f;

        [Tooltip("Strokes each player draws per round.")]
        [Range(1, 6)] public int StrokesPerPlayer = 3;

        [Tooltip("Ride-dot reach: latch radius scale for guide rings (ring radius = max(18, reach*1.8)).")]
        [Min(4f)] public float DotReach = 24f;

        [Header("Round timing (seconds)")]
        [Tooltip("Drawing phase time limit. The phase also ends early once every player finishes their strokes.")]
        [Min(10f)] public float DrawSeconds = 150f;

        [Tooltip("Voting phase time limit. Also ends early once every eligible human has voted.")]
        [Min(5f)] public float VoteSeconds = 25f;

        [Tooltip("Reveal display time before the next round's ready screen.")]
        [Min(2f)] public float RevealSeconds = 8f;

        [Header("Points")]
        [Tooltip("Awarded to a voter who guessed the artwork's subject.")]
        public int CorrectSubjectPoints = 1;

        [Tooltip("Awarded to a voter who guessed the fake artist.")]
        public int CorrectImposterPoints = 1;

        [Tooltip("Applied (once) to any player at least one voter accused of being the fake artist. Negative.")]
        public int GuessedPenalty = -1;

        [Tooltip("Awarded to the fake artist every round, caught or not.")]
        public int ImposterReward = 4;

        [Header("Vote")]
        [Tooltip("Number of subject options shown (correct + decoys).")]
        [Range(2, 8)] public int SubjectChoiceCount = 4;

        [Header("AI voters")]
        [Tooltip("Chance an AI voter picks the correct subject (otherwise a random decoy).")]
        [Range(0f, 1f)] public float AICorrectSubjectChance = 0.35f;

        [Tooltip("Chance an AI voter accuses the actual fake artist (otherwise a random other player).")]
        [Range(0f, 1f)] public float AICorrectImposterChance = 0.3f;
    }
}

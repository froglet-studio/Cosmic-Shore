using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The breath between rounds (P5, Loop Atlas loop <c>breath</c>; Garrett, 2026-11-21: "Build everything proposed"):
    /// the words of the one next step the scoreboard offers beside Play again, and where a crossing goes. One asset at
    /// <c>Assets/Resources/BreathConfig.asset</c>. Every line is authored; an empty line hides that offer.
    /// </summary>
    [CreateAssetMenu(fileName = "BreathConfig", menuName = "ScriptableObjects/Breath Config")]
    public class BreathConfigSO : ScriptableObject
    {
        public const string ResourcePath = "BreathConfig";

        [Header("Where a crossing goes")]
        [Tooltip("Every arcade card: the round's own card is found here to relaunch it at the next rung.")]
        [SerializeField] SO_GameList arcadeGames;

        [Tooltip("The arena cards: a round's crossing is the arena game its element trains for (BreathRules.ArenaFor).")]
        [SerializeField] SO_GameList arenaGames;

        [Tooltip("Rounds played this sitting before the breath offers a crossing instead of nothing (the sitting loop's " +
                 "'3 to 5 rounds a run').")]
        [SerializeField, Min(1)] int roundsBeforeCrossing = 3;

        [Header("Words")]
        [Tooltip("The next-rung offer. {intensity} is the rung.")]
        [SerializeField] string nextRungLine = "Next: Intensity {intensity}";

        [Tooltip("The crossing offer. {game} is the arena card's name.")]
        [SerializeField] string crossingLine = "Take it to {game}";

        [Tooltip("The Tide's daily job offer. {job} is the job's name.")]
        [SerializeField] string dailyJobLine = "Today's job: {job}";

        public SO_GameList ArcadeGames => arcadeGames;
        public SO_GameList ArenaGames => arenaGames;
        public int RoundsBeforeCrossing => roundsBeforeCrossing;
        public string NextRungLine => nextRungLine;
        public string CrossingLine => crossingLine;
        public string DailyJobLine => dailyJobLine;

        /// <summary>The card for <paramref name="mode"/>, from the arcade list then the arena list; null when neither has it.</summary>
        public SO_ArcadeGame Card(GameModes mode) => Find(arcadeGames, mode) ?? Find(arenaGames, mode);

        static SO_ArcadeGame Find(SO_GameList list, GameModes mode)
        {
            if (!list || list.Games == null) return null;
            foreach (var g in list.Games)
                if (g && g.Mode == mode) return g;
            return null;
        }

        static BreathConfigSO _cached;

        /// <summary>The project's asset, cached; null (and one warning) when it is missing, so no offer is drawn.</summary>
        public static BreathConfigSO Load()
        {
            if (_cached) return _cached;
            _cached = Resources.Load<BreathConfigSO>(ResourcePath);
            if (!_cached) CSDebug.LogWarning($"[Breath] Resources/{ResourcePath} is missing - the scoreboard offers no next step.");
            return _cached;
        }
    }
}

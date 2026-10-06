using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How each lobby AI difficulty handicaps the Skim Race pilot (<see cref="SkimRaceHandicap"/>).
    /// ONE setting per difficulty for every intensity: the mistakes are tuned once, on intensity 2
    /// (Easy about 120 s, Medium about 95 s, Hard the unhandicapped pilot), and every other track -
    /// including one added later - gets the same mistakes, so its times scale with its length.
    ///
    /// <para>Hard has no fields on purpose: it is the shipped per-intensity policy with no handicap.
    /// Ships as <c>Resources/SkimRaceDifficulty.asset</c>, written by
    /// <c>Tools/Build/author_skimrace_ai_config.py</c> (its DIFFICULTY table; <c>--check</c> also
    /// holds the field defaults below equal to that table, so a missing asset flies the same
    /// numbers). How the values were found: <c>Docs/SKIM_RACE_AI.md</c> §10.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SkimRaceDifficulty", menuName = "ScriptableObjects/AI/Skim Race Difficulty")]
    public class SkimRaceDifficultySO : ScriptableObject
    {
        public const string ResourcePath = "SkimRaceDifficulty";

        [Header("Easy")]
        [Tooltip("Seconds before an Easy pilot notices a new crystal (it keeps flying the track until " +
                 "then). Varies per crystal, half to one and a half times this.")]
        [Min(0f)] public float EasyReactionSeconds = 0.5f;

        [Tooltip("Chance per crystal that an Easy pilot misjudges the pass, flies over the crystal and " +
                 "has to turn back for it.")]
        [Range(0f, 1f)] public float EasyMistakeChance = 0.099f;

        [Header("Medium")]
        [Tooltip("Seconds before a Medium pilot notices a new crystal.")]
        [Min(0f)] public float MediumReactionSeconds = 0.25f;

        [Tooltip("Chance per crystal that a Medium pilot misjudges the pass.")]
        [Range(0f, 1f)] public float MediumMistakeChance = 0.045f;

        /// <summary>The handicap for <paramref name="difficulty"/>; none for Hard (and for any value
        /// that is not a difficulty, which resolves to the default first).</summary>
        public SkimRaceHandicapLevel For(AIDifficulty difficulty) => AIDifficultyRules.Resolve(difficulty) switch
        {
            AIDifficulty.Easy => new SkimRaceHandicapLevel(EasyReactionSeconds, EasyMistakeChance),
            AIDifficulty.Medium => new SkimRaceHandicapLevel(MediumReactionSeconds, MediumMistakeChance),
            _ => default,
        };

        /// <summary>The authored asset, or an instance carrying the field defaults when it is missing.</summary>
        public static SkimRaceDifficultySO Load()
        {
            var settings = Resources.Load<SkimRaceDifficultySO>(ResourcePath);
            return settings != null ? settings : CreateInstance<SkimRaceDifficultySO>();
        }
    }
}

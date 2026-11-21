using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Kelpwood's tunables other than the bite target (EndConditionOverridesSO) and the bite value
    /// (KelpwoodScoringRuleSO): progress feedback and the AI hunters. The arena's herds and omni crystals are the
    /// cell's (its spawn profile and the scene's CrystalManager). Config separation - authored by
    /// <c>Tools/Build/author_kelpwood_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity: intensity is the MAP, difficulty the OPPONENT (<c>AIDifficulty.cs</c>). Every ladder is monotone
    /// toward harder; the generator asserts it and that each C# initializer below matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "KelpwoodSettings",
        menuName = "ScriptableObjects/Arcade/KelpwoodSettings")]
    public class KelpwoodSettingsSO : ScriptableObject
    {
        [Header("Progress feedback")]
        [Tooltip("Seconds between the AI's slow checks (refuel targets) and the hint's idle check.")]
        [Min(0.1f)] public float progressSampleSeconds = 0.5f;

        [Header("AI hunters - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between re-choosing which rival to hunt (and re-rolling the aim error).")]
        public float[] aiRetargetSeconds = { 1.2f, 0.9f, 0.6f, 0.4f };

        [Tooltip("The farthest rival (u) the AI hunts. Past it, it holds the kelp's core.")]
        public float[] aiHuntRange = { 400f, 550f, 700f, 900f };

        [Tooltip("Seconds of the rival's own velocity the AI leads its nose by.")]
        public float[] aiLeadSeconds = { 0f, 0.1f, 0.2f, 0.3f };

        [Tooltip("How far (u) off the rival the AI's nose is aimed, in a direction re-rolled each retarget. The eraser " +
                 "is a 1.5 degree cone, so at 200 u anything past ~5 u misses: a wide aim is a real miss.")]
        public float[] aiAimError = { 16f, 9f, 4f, 1f };

        [Tooltip("The AI presses the eraser only while the rival is within this many degrees of its nose. The lock " +
                 "itself happens only inside the beam's own cone, so a wider gate means more wasted presses.")]
        public float[] aiLockGateDegrees = { 6f, 4f, 2.5f, 1.5f };

        [Tooltip("Seconds between two of the AI's lock presses (each lock spends one creature).")]
        public float[] aiLockIntervalSeconds = { 3f, 2f, 1.3f, 0.8f };

        [Tooltip("The AI does not lock again while this many of its creatures are already out hunting.")]
        public float[] aiMaxHuntersOut = { 1f, 2f, 3f, 4f };

        [Tooltip("Chance the AI breaks off when it is SILENCED (it cannot score until the silence ends) - to the " +
                 "nearest omni crystal for a creature, or away from its rival - instead of chasing on.")]
        public float[] aiBreakOffWhenSilencedChance = { 0f, 0.5f, 1f, 1f };

        [Tooltip("How much a SILENCED rival is preferred (it cannot score back): its cost is divided by this. 1 = no " +
                 "preference.")]
        public float[] aiSilencedPreyFocus = { 1f, 1f, 1.5f, 2f };

        [Header("AI hunters - shared")]
        [Tooltip("Seconds the AI holds the eraser per lock press. Long enough for the beam to meet the rival; a lock " +
                 "happens once per press however long it is held.")]
        [Min(0.05f)] public float aiLockHoldSeconds = 0.3f;

        [Tooltip("An AI with fewer tamed creatures than this goes for the nearest omni crystal first.")]
        [Min(0f)] public float aiRefuelBelowSchool = 2f;

        [Tooltip("How strongly the AI prefers HUMAN rivals: an AI rival must be this many times cheaper to steal " +
                 "the hunt. 1 = no preference.")]
        [Min(1f)] public float aiHumanFocus = 2f;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}

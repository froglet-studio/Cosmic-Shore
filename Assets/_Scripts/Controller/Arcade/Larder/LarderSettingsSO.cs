using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Larder's tunables other than the missile target (EndConditionOverridesSO) and the hit value
    /// (LarderScoringRuleSO): the AI hunters. The arena is the Boneyard's (Dog Fight's wreckage) and the crystals the
    /// scene's CrystalManager's. Config separation - authored by <c>Tools/Build/author_larder_assets.py</c>, never by
    /// hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity. Every ladder is monotone toward harder; the generator asserts it and that each C# initializer below
    /// matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "LarderSettings", menuName = "ScriptableObjects/Arcade/LarderSettings")]
    public class LarderSettingsSO : ScriptableObject
    {
        [Header("AI hunters - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between re-choosing which rival to hunt (and re-rolling the aim error).")]
        public float[] aiRetargetSeconds = { 2.5f, 1.8f, 1.2f, 0.8f };

        [Tooltip("Seconds of the rival's own velocity the AI leads its nose by.")]
        public float[] aiLeadSeconds = { 0f, 0.2f, 0.4f, 0.6f };

        [Tooltip("How far (u) off the rival the AI's nose is aimed, in a direction re-rolled each retarget. The lock " +
                 "cone is 5 degrees at Space 0 (keep: x1.6), so at 400 u anything past ~50 u loses the paint.")]
        public float[] aiAimError = { 70f, 40f, 18f, 6f };

        [Tooltip("With fewer missiles ready than this across its plants, the AI goes for the nearest crystal first " +
                 "(every crystal is a new missile plant).")]
        public float[] aiReloadBelowMissiles = { 1f, 2f, 3f, 4f };

        [Header("AI hunters - shared")]
        [Tooltip("The farthest crystal (u) the AI will fly to for a new plant. The Shrike cruises at 18 u/s: a far " +
                 "crystal is a sling, not a flight.")]
        [Min(0f)] public float aiCrystalRange = 700f;

        [Tooltip("How strongly the AI prefers HUMAN rivals: an AI rival must be this many times nearer to steal the " +
                 "hunt. 1 = no preference.")]
        [Min(1f)] public float aiHumanFocus = 1.5f;
    }
}

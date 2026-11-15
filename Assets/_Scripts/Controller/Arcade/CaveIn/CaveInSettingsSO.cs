using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cave-in's tunables other than the prism target (EndConditionOverridesSO): the AI miners' steering. The arena is
    /// Shatterplane's plates (read-only), the omni crystals the scene's CrystalManager's. Config separation - authored
    /// by <c>Tools/Build/author_cavein_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity. Every ladder is monotone toward harder; the generator asserts it and that each C# initializer below
    /// matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "CaveInSettings", menuName = "ScriptableObjects/Arcade/CaveInSettings")]
    public class CaveInSettingsSO : ScriptableObject
    {
        [Header("AI miners - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between the AI's choices of where to go (a fresh point on its plate, the next plate, a crystal).")]
        public float[] aiRetargetSeconds = { 3f, 2.4f, 1.8f, 1.3f };

        [Tooltip("How many choices the AI spends mining one plate before it moves on.")]
        public float[] aiChoicesPerPlate = { 2f, 3f, 4f, 5f };

        [Tooltip("Chance the AI's next plate is the one LINED UP behind its last (so what its solitons fire out runs on " +
                 "into more plate) rather than simply the nearest.")]
        public float[] aiLineUpChance = { 0.2f, 0.4f, 0.6f, 0.8f };

        [Tooltip("Chance the AI goes RAIDING when a rival is in range: behind them, where their freshest trail is (a " +
                 "rival's mass is hostile and scores too).")]
        public float[] aiRaidChance = { 0.05f, 0.1f, 0.15f, 0.2f };

        [Tooltip("The farthest rival (u) the AI raids.")]
        public float[] aiRaidRange = { 250f, 350f, 450f, 550f };

        [Tooltip("How many seconds of a rival's course BEHIND them the AI steers for: their trail, just laid.")]
        public float[] aiRaidBehindSeconds = { 0.5f, 1f, 1.5f, 2f };

        [Tooltip("How far (u) off its chosen point the AI steers, re-rolled each choice.")]
        public float[] aiAimError = { 120f, 80f, 45f, 20f };

        [Header("AI miners - shared")]
        [Tooltip("The farthest omni crystal (u) the AI turns aside for.")]
        [Min(0f)] public float aiCrystalReach = 300f;

        [Tooltip("How far out across a plate's face (as a share of its radius) the AI's mining points reach.")]
        [Range(0f, 1f)] public float aiFaceSpread = 0.8f;

        [Tooltip("The farthest plate (u) behind its last one the AI counts as lined up; with none, it takes the nearest plate.")]
        [Min(0f)] public float aiPlateRange = 900f;
    }
}

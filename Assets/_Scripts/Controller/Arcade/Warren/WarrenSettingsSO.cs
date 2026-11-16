using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Warren's tunables other than the round length (EndConditionOverridesSO): the lead feedback and the AI burrowers'
    /// steering. The arena is the bare Barren cell, the omni crystals the scene's CrystalManager's, the starting elements
    /// the card's. Config separation - authored by <c>Tools/Build/author_warren_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity. Every ladder is monotone toward harder; the generator asserts it and that each C# initializer below
    /// matches the authored asset.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "WarrenSettings", menuName = "ScriptableObjects/Arcade/WarrenSettings")]
    public class WarrenSettingsSO : ScriptableObject
    {
        [Header("Lead feedback")]
        [Tooltip("Seconds between the server's looks at who leads (for the lead-change toast).")]
        [Min(0.1f)] public float leadSampleSeconds = 1f;

        [Tooltip("No lead-change toast before this many seconds into the round (the opening is noise).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 15f;

        [Tooltip("At least this many seconds between two lead-change toasts.")]
        [Min(0f)] public float leadAnnounceMinGapSeconds = 8f;

        [Header("AI burrowers - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between the AI's choices of where to go (a raid, a crystal or open space to lay in).")]
        public float[] aiRetargetSeconds = { 4f, 3f, 2.2f, 1.6f };

        [Tooltip("Chance the AI goes RAIDING when a rival is in range: behind them, where their freshest mass is, so its " +
                 "next soliton swallows it.")]
        public float[] aiRaidChance = { 0.15f, 0.3f, 0.45f, 0.6f };

        [Tooltip("The farthest rival (u) the AI raids.")]
        public float[] aiRaidRange = { 300f, 450f, 600f, 800f };

        [Tooltip("How many seconds of a rival's course BEHIND them the AI steers for: their trail, just laid.")]
        public float[] aiRaidBehindSeconds = { 0.5f, 1f, 1.5f, 2f };

        [Tooltip("How far (u) off a raid point the AI steers, re-rolled each choice.")]
        public float[] aiAimError = { 80f, 50f, 25f, 10f };

        [Header("AI burrowers - shared")]
        [Tooltip("The farthest omni crystal (u) the AI turns aside for.")]
        [Min(0f)] public float aiCrystalReach = 450f;

        [Tooltip("How far (u) outside the nucleus the AI's laying points start.")]
        [Min(0f)] public float aiLayClearance = 120f;

        [Tooltip("How far out the AI's laying points reach, as a share of the membrane's radius.")]
        [Range(0.1f, 1f)] public float aiLayOuterFraction = 0.75f;
    }
}

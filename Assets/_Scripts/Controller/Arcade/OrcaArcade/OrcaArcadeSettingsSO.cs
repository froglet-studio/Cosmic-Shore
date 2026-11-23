using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The tunables of the Orca's three element games (Pod Hunt, Ice Shelf, Depth Charge) other than each one's target
    /// or round length (EndConditionOverridesSO) and point values (its scoring rule): the server's sample rate, the lead
    /// feedback and the AI HELM that <see cref="OrcaArcadeHelm"/> flies. One type, one asset per mode
    /// (<c>PodHuntSettings</c>, <c>IceShelfSettings</c>, <c>DepthChargeSettings</c>), so each game tunes on its own.
    /// Authored by <c>Tools/Build/orca_games_common.py</c> through each mode's generator, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity (<c>AIDifficulty.cs</c>). Every ladder is monotone toward harder; the generators assert it and that each
    /// C# initializer below matches the authored assets.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "OrcaArcadeSettings",
        menuName = "ScriptableObjects/Arcade/OrcaArcadeSettings")]
    public class OrcaArcadeSettingsSO : ScriptableObject
    {
        [Header("Server sampling")]
        [Tooltip("Seconds between the server's slow checks: the helm's goal, the lead toast, and (Ice Shelf) the census " +
                 "of the mass standing.")]
        [Min(0.1f)] public float sampleSeconds = 0.5f;

        [Header("Lead feedback (timed games)")]
        [Tooltip("No lead-change toast before this many seconds into the round (the opening is not a change).")]
        [Min(0f)] public float leadAnnounceAfterSeconds = 10f;

        [Tooltip("At least this many seconds between two lead-change toasts.")]
        [Min(0f)] public float leadAnnounceMinGapSeconds = 8f;

        [Header("AI helm - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("Seconds between re-choosing the helm's goal (the rival to stand off, the mass to raid, the next point " +
                 "of the shelf).")]
        public float[] aiRetargetSeconds = { 2.5f, 1.8f, 1.2f, 0.8f };

        [Tooltip("How far (u) the helm looks for its goal. Past it, it holds the cell's centre.")]
        public float[] aiScanRange = { 500f, 650f, 800f, 1000f };

        [Tooltip("The range (u) the helm holds from its goal. Every gunner's missile reaches ~720 u and the AI gunners " +
                 "pick prisms only inside 250 u, so a nearer stand-off is more fire on target and more fire taken.")]
        public float[] aiStandoff = { 320f, 260f, 210f, 170f };

        [Tooltip("Seconds of the goal's own velocity the helm leads its stand-off point by.")]
        public float[] aiLeadSeconds = { 0f, 0.2f, 0.4f, 0.6f };

        [Header("AI helm - shared")]
        [Tooltip("When every gunner's magazine holds fewer missiles than this, the helm goes for the nearest omni crystal " +
                 "first (an omni crystal gives every seat a missile).")]
        [Min(0f)] public float aiRefuelBelowMissiles = 1f;

        [Tooltip("How strongly the helm prefers HUMAN rivals: an AI rival must be this many times cheaper to steal the " +
                 "goal. 1 = no preference.")]
        [Min(1f)] public float aiHumanFocus = 1.5f;

        [Tooltip("Ice Shelf: the radius (u) of the loop the helm lays its shelf round, about its home point.")]
        [Min(10f)] public float aiShelfLoopRadius = 160f;

        [Tooltip("Ice Shelf: points on the shelf loop. The helm flies them in turn, so its trail closes a ring.")]
        [Min(3)] public int aiShelfLoopPoints = 6;

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }
    }
}

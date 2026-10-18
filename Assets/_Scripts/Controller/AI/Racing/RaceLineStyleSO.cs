using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How ONE hull races a line in ONE mode: how far out from the braid it flies, how much room its hull wants from a
    /// rail, and how risky it plays. Every hull moves differently (Yash, 2026-10-17: "no vessels move in the same way"),
    /// so each has its own asset, <c>Resources/RaceLines/&lt;Hull&gt;_&lt;Mode&gt;</c>, authored by
    /// <c>Tools/Build/author_race_lines.py</c> and later learned from people's flights of that hull.
    /// A hull with no asset in a mode flies the mode's shipped steering (Regatta: through each ring's centre).
    ///
    /// <para><b>Risk</b> is the careful-to-bold dial a player sets for themselves. Bold cuts more of each bend (a longer
    /// look-ahead) and asks less room of the rails; careful follows the line tightly with more room. Each AI seat draws
    /// its own risk round the hull's (<see cref="RiskFor"/>, seeded per seat), so one grid has careful and bold pilots of
    /// the same hull.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RaceLineStyle", menuName = "ScriptableObjects/AI/Race Line Style")]
    public sealed class RaceLineStyleSO : ScriptableObject
    {
        public const string ResourceFolder = "RaceLines";

        [Header("The line")]
        [Tooltip("Where the line runs between the braid and the ring's rim: 0 = the rails' own radius (22 u from the " +
                 "spine, in the gap between two rails), 1 = as far out as the tightest ring mouth allows with the hull's room.")]
        [Range(0f, 1f)] public float LaneShare = 0.6f;
        [Tooltip("Room the hull wants from a rail or the ring's rim, units (its size plus a margin), at middle risk.")]
        [Min(0f)] public float HullClearance = 12f;

        [Header("Risk")]
        [Tooltip("0 = careful (follows the line tightly, keeps 1.5x the room), 1 = bold (cuts each bend, keeps 0.75x).")]
        [Range(0f, 1f)] public float Risk = 0.5f;
        [Tooltip("Each AI seat draws its own risk within this much of the hull's, so seats of one hull differ.")]
        [Range(0f, 0.5f)] public float RiskSpread = 0.15f;
        [Tooltip("Look-ahead along the line, seconds of travel at the current speed, for a careful pilot.")]
        [Min(0f)] public float CarefulLookaheadSeconds = 0.35f;
        [Tooltip("Look-ahead along the line, seconds of travel, for a bold pilot (cuts more of each bend).")]
        [Min(0f)] public float BoldLookaheadSeconds = 0.8f;
        [Tooltip("Look-ahead floor and ceiling, units.")]
        [Min(1f)] public float MinLookahead = 60f;
        [Min(1f)] public float MaxLookahead = 520f;

        /// <summary>This hull's style in this mode, or null (the mode's shipped steering then applies).</summary>
        public static RaceLineStyleSO Load(VesselClassType hull, GameModes mode) =>
            Resources.Load<RaceLineStyleSO>($"{ResourceFolder}/{hull}_{mode}");

        /// <summary>A seat's own risk: the hull's, plus a seeded draw within <see cref="RiskSpread"/>.</summary>
        public float RiskFor(int seatSeed)
        {
            var rng = new System.Random(seatSeed);
            return Mathf.Clamp01(Risk + RiskSpread * (float)(rng.NextDouble() * 2.0 - 1.0));
        }

        /// <summary>Room at <paramref name="risk"/>: 1.5x the hull's clearance when careful, 0.75x when bold.</summary>
        public float ClearanceAt(float risk) => HullClearance * Mathf.Lerp(1.5f, 0.75f, Mathf.Clamp01(risk));

        /// <summary>
        /// The line's distance from the spine: the rails' radius plus <see cref="LaneShare"/> of what is left inside the
        /// tightest mouth once the hull's room is kept. Never past the mouth less the room, never inside the rails.
        /// </summary>
        public float LaneRadius(float tightestMouth, float railOffset, float risk)
        {
            float room = Mathf.Max(0f, tightestMouth - ClearanceAt(risk) - railOffset);
            return railOffset + LaneShare * room;
        }

        /// <summary>How far ahead along the line the pilot aims, units, at <paramref name="speed"/>.</summary>
        public float Lookahead(float speed, float risk) =>
            Mathf.Clamp(Mathf.Abs(speed) * Mathf.Lerp(CarefulLookaheadSeconds, BoldLookaheadSeconds, Mathf.Clamp01(risk)),
                        MinLookahead, Mathf.Max(MinLookahead, MaxLookahead));
    }
}

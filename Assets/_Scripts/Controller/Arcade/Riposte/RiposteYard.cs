using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where every piece of Riposte's arena stands and why (RIPOSTE.md section 4): the placement rules of
    /// <see cref="SpawnableRiposteYard"/> as pure functions, so each rule has a test (<c>RiposteYardTests</c>) and a
    /// line-for-line mirror in <c>Tools/Build/riposte_yard_budget.py</c>.
    ///
    /// <para>Each rule exists for something the Swordfish's kit does. A <b>lane</b> is a straight, stick-free tube
    /// from the centre clearing out through a refuel clearing: the ramp winds up only on a straight line, and a sword
    /// hit lands only on a slower hull. A <b>rail</b> is a row of pickets parallel to a lane, just outside it: the
    /// bill cuts them lengthwise, which is ramp fuel earned without leaving the line. A <b>gate</b> is a jack on a
    /// lane's axis with its four cross arms shielded: it breaks the line unless it is threaded or struck twice.
    /// <b>Thorns</b> are red tips on the loose sticks nearest a lane wall: drifting off the line costs. The
    /// <b>ironwood core</b> is every loose stick near the centre: where the lanes meet, a blade that is not energized
    /// binds.</para>
    /// </summary>
    public static class RiposteYard
    {
        /// <summary>Prisms in one rail picket (half a loose stick), and the air between two pickets of a rail (u).</summary>
        public const int PicketPrisms = 6;
        public const float PicketGap = 15f;
        /// <summary>How far outside the lane's wall a rail's axis runs (u): two sections, so the lane itself stays empty.</summary>
        public const float RailStandoff = 6f;
        /// <summary>Air (u) between the centre clearing's edge and a rail's first picket.</summary>
        public const float RailStart = 10f;
        /// <summary>Air (u) a lane's refuel clearing keeps from the heap's edge.</summary>
        public const float ClearingInset = 10f;

        public static float PicketLength => PicketPrisms * SpawnableJackstraw.Segment;

        /// <summary>The unit direction of lane <paramref name="lane"/>: it runs from the centre through ring clearing
        /// <paramref name="lane"/> of <paramref name="clearings"/>, so every lane ends at a refuel.</summary>
        public static Vector3 LaneAxis(int lane, int clearings) =>
            SpawnableJackstraw.Fibonacci(lane, Mathf.Max(1, clearings));

        /// <summary>Two unit vectors square to <paramref name="axis"/> and to each other.</summary>
        public static void Basis(Vector3 axis, out Vector3 e1, out Vector3 e2)
        {
            var helper = Mathf.Abs(axis.z) < 0.9f ? Vector3.forward : Vector3.right;
            e1 = Vector3.Cross(axis, helper).normalized;
            e2 = Vector3.Cross(axis, e1);
        }

        /// <summary>The offset of rail <paramref name="rail"/> of <paramref name="rails"/> from its lane's axis: evenly
        /// round the lane, each lane turned by the golden angle so no two lanes' rails line up.</summary>
        public static Vector3 RailOffset(Vector3 axis, int lane, int rail, int rails, float laneRadius)
        {
            Basis(axis, out var e1, out var e2);
            float angle = 2f * Mathf.PI * rail / Mathf.Max(1, rails) + 2.39996323f * lane;
            return (e1 * Mathf.Cos(angle) + e2 * Mathf.Sin(angle)) * (laneRadius + RailStandoff);
        }

        /// <summary>Distance (u) along the lane of picket <paramref name="index"/>'s centre.</summary>
        public static float PicketCentre(float centreClearingRadius, int index) =>
            centreClearingRadius + RailStart + PicketLength * 0.5f + index * (PicketLength + PicketGap);

        /// <summary>True when lane <paramref name="lane"/> carries gates: every <paramref name="everyNth"/>th lane, 0 = none.</summary>
        public static bool GatedLane(int lane, int everyNth) => everyNth > 0 && lane % everyNth == 0;

        /// <summary>Distance (u) along the lane of gate <paramref name="gate"/> of <paramref name="gates"/>: evenly
        /// between the centre clearing's edge (<paramref name="from"/>) and the refuel clearing's (<paramref name="to"/>).</summary>
        public static float GateDistance(float from, float to, int gate, int gates) =>
            from + (to - from) * (gate + 1) / (gates + 1);

        /// <summary>True when a loose stick this far from a lane's axis carries red tips.</summary>
        public static bool Thorned(float distanceToLaneAxis, float laneRadius, float thornBand) =>
            thornBand > 0f && distanceToLaneAxis < laneRadius + thornBand;

        /// <summary>True when a loose stick centred here has an ironwood heart.</summary>
        public static bool Ironwood(Vector3 stickCentre, float ironwoodCoreRadius) =>
            ironwoodCoreRadius > 0f && stickCentre.sqrMagnitude < ironwoodCoreRadius * ironwoodCoreRadius;

        /// <summary>Squared least distance from <paramref name="p"/> to segment a-b.</summary>
        public static float PointSegmentSqr(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (a + ab * t - p).sqrMagnitude;
        }
    }
}

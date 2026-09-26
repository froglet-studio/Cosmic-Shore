using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The three geometric questions a <see cref="FoldGate"/> asks, as pure functions of a pose
    /// and a segment. Extracted from the MonoBehaviour for one reason: the ARMING rule is the
    /// piece of this ability where a mistake is not a nuance but "every fold teleports you
    /// straight back", and a rule that cannot be run offline is a rule nobody has watched work.
    /// `Tools/Build/foldgate_harness/` compiles and runs this file verbatim.
    ///
    /// <para>All three take the gate's frame explicitly (<paramref name="centre"/>,
    /// <paramref name="axis"/>, <paramref name="radius"/>) rather than reading a Transform, which
    /// is what makes them testable and what keeps the pair's two ends symmetric - a gate and its
    /// partner run the same functions against different frames.</para>
    /// </summary>
    public static class FoldGateGeometry
    {
        /// <summary>
        /// How deep the "standing in the mouth" zone runs along the axis. At least one mouth
        /// radius, so a gate always owns a region rather than a plane, and at least one exit
        /// clearance, so the point a transit deposits a pilot at is inside it BY CONSTRUCTION -
        /// which is what makes disarming at the far end sufficient rather than approximately
        /// right.
        /// </summary>
        public static float NearZoneDepth(float radius, float exitClearance)
            => Mathf.Max(exitClearance, radius);

        /// <summary>
        /// Is this point close enough to the mouth to count as standing in it? A cylinder about
        /// the axis, one mouth wide and <see cref="NearZoneDepth"/> deep.
        /// </summary>
        public static bool InNearZone(Vector3 p, Vector3 centre, Vector3 axis,
                                      float radius, float exitClearance)
        {
            Vector3 rel = p - centre;
            float axial = Vector3.Dot(rel, axis);
            if (Mathf.Abs(axial) > NearZoneDepth(radius, exitClearance)) return false;
            Vector3 lateral = rel - axial * axis;
            return lateral.sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// Did the segment prev-&gt;cur cross the gate's plane INSIDE the mouth? Direction
        /// agnostic, exactly as <see cref="ScarabSwitch"/> tests a ball: threading a switch
        /// backwards is still threading it. <paramref name="hit"/> is where it crossed.
        /// </summary>
        public static bool CrossedMouth(Vector3 prev, Vector3 cur, Vector3 centre, Vector3 axis,
                                        float radius, out Vector3 hit)
        {
            hit = default;
            float dPrev = Vector3.Dot(prev - centre, axis);
            float dCur = Vector3.Dot(cur - centre, axis);
            if (dPrev * dCur > 0f) return false;                 // same side - no crossing
            if (Mathf.Approximately(dPrev, dCur)) return false;

            float t = Mathf.Clamp01(dPrev / (dPrev - dCur));
            hit = Vector3.Lerp(prev, cur, t);
            Vector3 rel = hit - centre;
            Vector3 lateral = rel - Vector3.Dot(rel, axis) * axis;
            return lateral.sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// Where a pilot who crossed <paramref name="hit"/> on the near gate comes out of the far
        /// one. Two properties are preserved, and together they are what make a portal predictable
        /// rather than a shuffle: the lateral offset inside the mouth (enter near the rim, leave
        /// near the rim) and the SENSE you were travelling (the side you were heading for is the
        /// side you emerge on, so momentum reads through the gate).
        /// </summary>
        public static Vector3 Exit(Vector3 hit, Vector3 travel,
                                   Vector3 nearCentre, Vector3 nearAxis,
                                   Vector3 farCentre, Vector3 farAxis, float exitClearance)
        {
            Vector3 rel = hit - nearCentre;
            Vector3 lateral = rel - Vector3.Dot(rel, nearAxis) * nearAxis;
            float sense = Vector3.Dot(travel, nearAxis) >= 0f ? 1f : -1f;
            return farCentre + lateral + farAxis * (sense * exitClearance);
        }
    }
}

using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The geometric questions a <see cref="FoldGate"/> asks — is this pilot standing in the
    /// mouth, did this step cross it, and where does a point come out on the other side — as pure
    /// functions of a pose and a segment. Extracted from the MonoBehaviour for one reason: the ARMING rule is the
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
        /// radius, so a gate always owns a region rather than a plane. A transit deposits a pilot
        /// exactly as far past the far plane as their last step took them past the near one
        /// (<see cref="Through"/>), so for any step shorter than this depth the arrival point is
        /// inside the far gate's zone BY CONSTRUCTION — which is what makes disarming there
        /// sufficient. A longer step (a hitch at speed) lands outside it, and that is harmless
        /// rather than a bounce: the pilot is travelling AWAY from the far plane, so the only way
        /// to cross it again is to turn round and fly back through, which is a real transit.
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
        /// Carry a point through the portal: its position relative to the NEAR mouth becomes the
        /// same position relative to the FAR mouth — lateral offset and axial depth alike.
        ///
        /// <para><b>This is what makes a transit SEAMLESS rather than merely predictable.</b> An
        /// earlier cut re-projected the crossing point onto the far plane and then pushed the
        /// pilot a fixed clearance past it, which preserved the lateral offset and the travel
        /// sense but moved the pilot a further ~40 units along the axis on the frame of the
        /// jump — a lurch forward that no camera or ribbon could hide. Mapping the pilot's actual
        /// position instead makes the jump a pure change of frame: the pilot is exactly as far
        /// past the far plane as they were past the near one, so every other system that follows
        /// the vessel (the camera, the tail, the jets) can be carried through by the SAME map and
        /// arrive where it would have been had the two gates been one.</para>
        ///
        /// <para>A pair shares ONE axis by construction (a fold lays both ends from one heading),
        /// so between the two frames this is a pure TRANSLATION by <c>farCentre - nearCentre</c> —
        /// no rotation, which is why the vessel's attitude, its momentum and the camera's own
        /// smoothing state all pass through untouched. The axes are still taken explicitly so the
        /// map stays defined (lateral kept, axial re-laid along the far axis) if that ever
        /// changes.</para>
        ///
        /// <para>It is also its own inverse through the partner: mapping near-to-far and then
        /// far-to-near returns the point exactly, which is what lets the camera, the corridor and
        /// the ribbon break reason in either gate's frame.</para>
        /// </summary>
        public static Vector3 Through(Vector3 p, Vector3 nearCentre, Vector3 nearAxis,
                                      Vector3 farCentre, Vector3 farAxis)
        {
            Vector3 rel = p - nearCentre;
            float axial = Vector3.Dot(rel, nearAxis);
            Vector3 lateral = rel - axial * nearAxis;
            return farCentre + lateral + farAxis * axial;
        }

        /// <summary>
        /// The point on the gate's plane closest to <paramref name="p"/> — where a pilot who is
        /// just past the mouth actually went through it. Used to end one ribbon and start the
        /// next exactly AT the two mouths, so a tail reads as passing through the gate rather than
        /// stopping short of it.
        /// </summary>
        public static Vector3 OnPlane(Vector3 p, Vector3 centre, Vector3 axis)
            => p - Vector3.Dot(p - centre, axis) * axis;

        /// <summary>
        /// Signed distance from the gate's plane, positive on the side <paramref name="axis"/>
        /// points to.
        /// </summary>
        public static float Axial(Vector3 p, Vector3 centre, Vector3 axis)
            => Vector3.Dot(p - centre, axis);

        /// <summary>
        /// Distance from the gate's axis — how far off-centre a point is, whatever its depth.
        /// </summary>
        public static float Lateral(Vector3 p, Vector3 centre, Vector3 axis)
        {
            Vector3 rel = p - centre;
            return (rel - Vector3.Dot(rel, axis) * axis).magnitude;
        }
    }
}

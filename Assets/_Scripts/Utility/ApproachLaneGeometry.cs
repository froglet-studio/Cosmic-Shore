using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The APPROACH LANE: a straight line struck through a player's own spawn slot that passes the
    /// cell centre at a standoff instead of running into it. Pure math, no Unity scene state -
    /// unit tested in <c>ApproachLaneGeometryTests</c>, and called by
    /// <c>CrystalManager.LaneSlotPosition</c> so the shipped path IS the tested one.
    ///
    /// <para>Drumfire is the mode it was written for. The Dolphin's only weapon is armed by
    /// SKIMMING and fired by touching a CRYSTAL, so a line of crystals is a line of triggers - and
    /// because the line runs PAST the target rather than at it, the target is always off to one
    /// side and every shot needs a deliberate turn off the flight vector. That is the whole
    /// fly / aim / shoot / repeat lesson, expressed as geometry rather than as a rule.</para>
    ///
    /// <para><b>The lane and the spawn ring are ONE arrangement.</b> Lane <c>k</c> is struck
    /// through <c>CellSpawnFormation.Direction(k, lanes, formation)</c> at
    /// <paramref name="ringRadius"/> - the same slot the pilot spawns on - so lane ownership is
    /// emergent and nothing has to be assigned. The caller must therefore author the same radius
    /// and the same formation the scene's spawner uses; <c>Tools/Build/author_drumfire_assets.py</c>
    /// asserts both.</para>
    /// </summary>
    public static class ApproachLaneGeometry
    {
        /// <summary>
        /// The lane's unit heading: from the spawn slot, inward past the centre at a standoff of
        /// <paramref name="offsetFromCenter"/>.
        ///
        /// <para>Derivation: the lane leaves a point on a sphere of <paramref name="ringRadius"/>,
        /// so a heading tilted <c>theta</c> off the straight-in direction has closest approach
        /// <c>ringRadius * sin(theta)</c>. Solving for the authored standoff gives
        /// <c>sin(theta) = offset / ringRadius</c> - which is why the offset is CLAMPED: an offset
        /// past the ring radius has no line through the slot that satisfies it, and the honest
        /// answer there is a tangent (the closest the geometry can get), never a NaN.</para>
        /// </summary>
        public static Vector3 Heading(Vector3 outward, float ringRadius, float offsetFromCenter)
        {
            float sinTheta = ringRadius > 0f
                ? Mathf.Clamp01(Mathf.Abs(offsetFromCenter) / ringRadius)
                : 0f;
            float cosTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - sinTheta * sinTheta));

            return (-outward * cosTheta + Perpendicular(outward) * sinTheta).normalized;
        }

        /// <summary>
        /// Where slot <paramref name="slot"/> of <paramref name="slotsPerLane"/> sits on lane
        /// <paramref name="lane"/> of <paramref name="lanes"/>.
        ///
        /// <para>The band is <paramref name="leadDistance"/> to
        /// <c>leadDistance + length</c> along the lane, evenly divided. Both numbers are authored
        /// rather than derived because WHERE on the lane the crystals sit decides how much of the
        /// target each shot can reach: a shot's yield falls as the square of its range, so a band
        /// centred on the lane's closest approach pays roughly evenly across its slots while a band
        /// that starts there front-loads the whole match into the first trigger.</para>
        /// </summary>
        public static Vector3 SlotPosition(
            Vector3 center,
            int lane,
            int lanes,
            int slot,
            int slotsPerLane,
            float ringRadius,
            float offsetFromCenter,
            float leadDistance,
            float length,
            CellSpawnFormation.Formation formation = CellSpawnFormation.Formation.Symmetric)
        {
            lanes = Mathf.Max(1, lanes);
            slotsPerLane = Mathf.Max(1, slotsPerLane);

            Vector3 outward = CellSpawnFormation.Direction(Mathf.Max(0, lane), lanes, formation);
            Vector3 heading = Heading(outward, ringRadius, offsetFromCenter);

            float spacing = slotsPerLane > 1 ? length / (slotsPerLane - 1) : 0f;
            float along = leadDistance + Mathf.Clamp(slot, 0, slotsPerLane - 1) * spacing;

            return center + outward * ringRadius + heading * along;
        }

        /// <summary>
        /// The lane a crystal belongs to. <b>Lane-MAJOR</b> (<c>lane = index / slotsPerLane</c>),
        /// not slot-major, and that is load-bearing: <c>NetworkCrystalManager</c> grows the slot
        /// list as players arrive and only fills entries that are still empty, so a mapping where
        /// a new player changed which lane the EXISTING crystals belong to would strand every one
        /// of them on the wrong line until it was next collected. Appending whole lanes cannot
        /// disturb the lanes already laid.
        /// </summary>
        public static int LaneOf(int index, int slotsPerLane, int lanes) =>
            (Mathf.Max(0, index) / Mathf.Max(1, slotsPerLane)) % Mathf.Max(1, lanes);

        /// <summary>The slot within its lane. See <see cref="LaneOf"/> for why the split is this way round.</summary>
        public static int SlotOf(int index, int slotsPerLane) =>
            Mathf.Max(0, index) % Mathf.Max(1, slotsPerLane);

        /// <summary>
        /// A unit vector perpendicular to <paramref name="outward"/>, chosen the same way every
        /// time so a lane is identical on every peer and across a respawn. WHICH perpendicular it
        /// is does not matter (the formation is symmetric); that it never CHANGES does.
        /// </summary>
        public static Vector3 Perpendicular(Vector3 outward)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(outward, Vector3.up)) > 0.99f
                ? Vector3.forward
                : Vector3.up;
            return Vector3.Cross(outward, reference).normalized;
        }
    }
}

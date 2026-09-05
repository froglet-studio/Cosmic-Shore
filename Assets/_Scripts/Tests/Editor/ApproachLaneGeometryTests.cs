using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The approach-lane contract, held against Drumfire's own shipped numbers.
    ///
    /// <para>Every assertion here is a property the MODE depends on, not a restatement of the
    /// formula: a lane must miss the drum (or a pilot flying their crystals flies into it), it
    /// must pass the drum somewhere in the MIDDLE of its crystal band (or the aiming lesson is
    /// only taught on the way in), the last crystal must still be inside the membrane, lanes must
    /// be one-per-spawn-slot, and growing the roster must not disturb the lanes already laid.</para>
    ///
    /// <para>The numbers are the ones <c>Tools/Build/author_drumfire_assets.py</c> writes onto
    /// <c>MinigameDrumfire</c>'s crystal manager and <c>Tools/Build/drumfire_arena.py</c> measures
    /// the arena against. If a playtest retunes them there, this suite is where the retune is
    /// checked for the things a measurement cannot see.</para>
    /// </summary>
    public class ApproachLaneGeometryTests
    {
        // ── Drumfire's shipped lane band (mirrors author_drumfire_assets.py) ──
        const float RingRadius = 1120f;
        const float Offset = 420f;
        const float Lead = 640f;
        const float Length = 800f;

        // The drum, from SpawnableDrum's serialized defaults.
        const float DrumRadius = 320f;

        // Cell membrane, from the Drumfire cell's membrane prefab (matches drumfire_arena.py).
        const float MembraneRadius = 1200f;

        static readonly Vector3 Center = new(12f, -7f, 4f);
        const float Tolerance = 1e-3f;

        static Vector3 Slot(int lane, int lanes, int slot, int slots) =>
            ApproachLaneGeometry.SlotPosition(Center, lane, lanes, slot, slots,
                RingRadius, Offset, Lead, Length);

        /// <summary>Distance from the cell centre to the infinite line through a lane.</summary>
        static float ClosestApproach(int lane, int lanes)
        {
            Vector3 outward = CellSpawnFormation.Direction(lane, lanes);
            Vector3 origin = Center + outward * RingRadius;
            Vector3 heading = ApproachLaneGeometry.Heading(outward, RingRadius, Offset);
            Vector3 toCenter = Center - origin;
            return Vector3.Distance(toCenter, Vector3.Project(toCenter, heading));
        }

        // ── The standoff is what makes the lane a firing lane ─────────────────

        [Test]
        public void Lane_PassesTheCentreAtExactlyTheAuthoredStandoff(
            [Values(1, 2, 3, 4, 6)] int lanes)
        {
            for (int lane = 0; lane < lanes; lane++)
                Assert.AreEqual(Offset, ClosestApproach(lane, lanes), 0.05f,
                    $"Lane {lane} of {lanes} misses its authored standoff, so its pilot would not " +
                    "fly the distance from the target the mode was tuned for.");
        }

        [Test]
        public void Lane_ClearsTheDrum([Values(1, 2, 3, 4, 6)] int lanes)
        {
            // If the lane's closest approach were inside the drum, a pilot flying their own
            // crystals would fly into the target - the one thing the standoff exists to prevent.
            for (int lane = 0; lane < lanes; lane++)
                Assert.Greater(ClosestApproach(lane, lanes), DrumRadius,
                    $"Lane {lane} of {lanes} runs through the drum.");
        }

        [Test]
        public void Lane_StaysCloseEnoughToBeWorthShootingAt()
        {
            // The other side of the same trade: a standoff far outside the drum turns every shot
            // into a long-range poke, and blast yield falls as the square of range.
            Assert.Less(ClosestApproach(0, 4), DrumRadius * 2f,
                "The lane stands so far off the drum that a full-energy blast barely reaches it.");
        }

        [Test]
        public void Heading_ClampsRatherThanFailingWhenTheStandoffExceedsTheRing()
        {
            // A standoff past the ring radius has NO line through the slot that satisfies it. The
            // honest answer is the tangent - never a NaN quietly placing crystals at the origin.
            Vector3 outward = Vector3.forward;
            Vector3 heading = ApproachLaneGeometry.Heading(outward, 100f, 5000f);

            Assert.AreEqual(1f, heading.magnitude, Tolerance, "Clamped heading is not a unit vector.");
            Assert.IsFalse(float.IsNaN(heading.x) || float.IsNaN(heading.y) || float.IsNaN(heading.z),
                "Clamped heading produced NaN.");
            Assert.AreEqual(0f, Vector3.Dot(heading, outward), Tolerance,
                "An over-clamped lane should run tangent to the ring, not back into it.");
        }

        // ── The crystal band sits ON the closest approach ─────────────────────

        [Test]
        public void CrystalBand_StraddlesTheLanesClosestApproach([Values(5, 6, 7, 8)] int slots)
        {
            // Blast yield falls as range squared, so a band that STARTS at the closest approach
            // front-loads the whole match into its first crystal and its last shots do nothing.
            // Centring the band on the pass is what makes every trigger worth the same.
            Vector3 outward = CellSpawnFormation.Direction(0, 4);
            Vector3 origin = Center + outward * RingRadius;
            Vector3 heading = ApproachLaneGeometry.Heading(outward, RingRadius, Offset);
            float tClosest = Vector3.Dot(Center - origin, heading);

            Assert.Greater(tClosest, Lead,
                "Every crystal is laid before the lane's closest approach - the pilot never gets " +
                "to shoot on the way out.");
            Assert.Less(tClosest, Lead + Length,
                "Every crystal is laid after the lane's closest approach - the pilot never gets " +
                "to shoot on the way in.");

            // And the band's own slots straddle it, not just its endpoints.
            float first = Vector3.Distance(Slot(0, 4, 0, slots), Center);
            float last = Vector3.Distance(Slot(0, 4, slots - 1, slots), Center);
            float mid = Vector3.Distance(Slot(0, 4, slots / 2, slots), Center);

            Assert.Less(mid, first, $"{slots}-slot band: the middle crystal is not closer than the first.");
            Assert.Less(mid, last, $"{slots}-slot band: the middle crystal is not closer than the last.");
        }

        [Test]
        public void CrystalBand_LeavesRunOutInsideTheMembraneAfterTheLastCrystal(
            [Values(5, 6, 7, 8)] int slots)
        {
            // The pilot is still travelling when they take the last crystal. If the lane left the
            // membrane at that moment they would take their final shot into a boundary, so the
            // lane has to keep going for a while after its last trigger.
            Vector3 outward = CellSpawnFormation.Direction(0, 4);
            Vector3 origin = Center + outward * RingRadius;
            Vector3 heading = ApproachLaneGeometry.Heading(outward, RingRadius, Offset);
            float tClosest = Vector3.Dot(Center - origin, heading);

            // Where the lane crosses the membrane, measured along the lane from the spawn slot.
            float tExit = tClosest + Mathf.Sqrt(MembraneRadius * MembraneRadius - Offset * Offset);

            Assert.Greater(tExit - (Lead + Length), 200f,
                $"{slots}-slot band: under 200u of run-out between the last crystal and the membrane.");

            // And every crystal is genuinely inside it.
            for (int s = 0; s < slots; s++)
                Assert.Less(Vector3.Distance(Slot(0, 4, s, slots), Center), MembraneRadius,
                    $"{slots}-slot band: crystal {s} is outside the membrane.");
        }

        [Test]
        public void CrystalBand_IsEvenlySpacedAndOrderedAlongTheLane([Values(5, 6, 7, 8)] int slots)
        {
            Vector3 previous = Slot(0, 4, 0, slots);
            float expected = Length / (slots - 1);

            for (int s = 1; s < slots; s++)
            {
                Vector3 here = Slot(0, 4, s, slots);
                Assert.AreEqual(expected, Vector3.Distance(previous, here), 0.05f,
                    $"Slot {s} of {slots} breaks the even spacing that gives the pilot a steady beat.");
                previous = here;
            }
        }

        [Test]
        public void CrystalBand_IsCollinear([Values(5, 8)] int slots)
        {
            // A "lane" that is not a straight line is not a lane.
            Vector3 a = Slot(0, 4, 0, slots);
            Vector3 dir = (Slot(0, 4, slots - 1, slots) - a).normalized;

            for (int s = 1; s < slots - 1; s++)
            {
                Vector3 offset = Slot(0, 4, s, slots) - a;
                Assert.AreEqual(0f, Vector3.Cross(offset, dir).magnitude, 0.05f,
                    $"Slot {s} of {slots} is off the line its own lane runs on.");
            }
        }

        [Test]
        public void SingleSlotLane_PlacesItsOneCrystalAtTheLeadDistance()
        {
            // slots == 1 divides by (slots - 1) in the spacing; the guard has to hold.
            Vector3 outward = CellSpawnFormation.Direction(0, 4);
            Vector3 expected = Center + outward * RingRadius
                               + ApproachLaneGeometry.Heading(outward, RingRadius, Offset) * Lead;

            Assert.AreEqual(0f, Vector3.Distance(expected, Slot(0, 4, 0, 1)), Tolerance,
                "A one-crystal lane did not put its crystal at the lead distance.");
        }

        // ── Lane ownership is emergent, and stable as the roster fills ────────

        [Test]
        public void LaneMapping_IsLaneMajorSoAGrowingRosterOnlyAppends()
        {
            // NetworkCrystalManager grows the slot list as players arrive and fills only the empty
            // entries. A slot-major mapping would re-home every crystal already laid.
            const int slots = 6;

            for (int index = 0; index < slots * 2; index++)
            {
                Assert.AreEqual(ApproachLaneGeometry.LaneOf(index, slots, 2),
                    ApproachLaneGeometry.LaneOf(index, slots, 4),
                    $"Crystal {index} changed lanes when the roster grew from 2 players to 4.");
            }
        }

        [Test]
        public void LaneMapping_GivesEachLaneAContiguousBlockOfCrystals()
        {
            const int slots = 5;
            const int lanes = 4;

            for (int lane = 0; lane < lanes; lane++)
                for (int s = 0; s < slots; s++)
                {
                    int index = lane * slots + s;
                    Assert.AreEqual(lane, ApproachLaneGeometry.LaneOf(index, slots, lanes),
                        $"Crystal {index} is not in lane {lane}'s own block.");
                    Assert.AreEqual(s, ApproachLaneGeometry.SlotOf(index, slots),
                        $"Crystal {index} is not at slot {s} of lane {lane}.");
                }
        }

        [Test]
        public void EachLane_StartsOnItsOwnSpawnSlot([Values(2, 3, 4)] int lanes)
        {
            // Lane ownership is not assigned anywhere - it is emergent, because lane k is struck
            // through spawn slot k. If that ever stopped being true, two pilots would share a lane
            // and a third would have none.
            var spawns = CellSpawnFormation.Build(lanes, Center, RingRadius);

            for (int lane = 0; lane < lanes; lane++)
            {
                Vector3 first = Slot(lane, lanes, 0, 6);
                Vector3 toFirst = (first - spawns[lane].position).normalized;
                Vector3 heading = ApproachLaneGeometry.Heading(
                    CellSpawnFormation.Direction(lane, lanes), RingRadius, Offset);

                Assert.AreEqual(1f, Vector3.Dot(toFirst, heading), 1e-2f,
                    $"Lane {lane} of {lanes} does not run out of its own spawn point.");
            }
        }

        [Test]
        public void Lanes_AreDistinct([Values(2, 3, 4)] int lanes)
        {
            for (int a = 0; a < lanes; a++)
                for (int b = a + 1; b < lanes; b++)
                    Assert.Greater(Vector3.Distance(Slot(a, lanes, 0, 6), Slot(b, lanes, 0, 6)), 1f,
                        $"Lanes {a} and {b} of {lanes} start at the same place.");
        }

        [Test]
        public void Lane_IsIdenticalOnEveryPeerAndAcrossARespawn()
        {
            // Nothing here may consult UnityEngine.Random or scene state: a crystal that reloads
            // somewhere else on the line is a crystal the pilot has to go looking for.
            Vector3 once = Slot(2, 4, 3, 6);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(0f, Vector3.Distance(once, Slot(2, 4, 3, 6)), 0f,
                    "Lane placement is not deterministic.");
        }

        [Test]
        public void Perpendicular_StaysUnitLengthEvenAtThePoles()
        {
            // The reference vector flips near the poles; the flip must not produce a zero cross.
            foreach (var outward in new[]
                     { Vector3.up, Vector3.down, Vector3.forward, Vector3.right,
                       new Vector3(0.001f, 1f, 0.001f).normalized })
            {
                Vector3 perpendicular = ApproachLaneGeometry.Perpendicular(outward);
                Assert.AreEqual(1f, perpendicular.magnitude, Tolerance,
                    $"Perpendicular of {outward} is not a unit vector.");
                Assert.AreEqual(0f, Vector3.Dot(perpendicular, outward), Tolerance,
                    $"Perpendicular of {outward} is not perpendicular.");
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Brittle Star (<c>R_VesselActions/BRITTLE_STAR.md</c>): the pure hull form (parts, accents, element morphs,
    /// winding, bounds) and the pure rules (the ring's life cycle clause by clause from the class document, mode
    /// selection, the aim math and the deterministic spread).
    /// </summary>
    [TestFixture]
    public class BrittleStarTests
    {
        static readonly BrittleStarHullForm.Settings S = BrittleStarHullForm.Defaults;
        const float RebuildSeconds = 0.5f;
        const float OverdriveSeconds = 6f;

        // ------------------------------------------------------------------ hull form

        [Test]
        public void Form_PartZeroIsTheCockpit_ThenTheRing_ThenSixFlames()
        {
            var parts = BrittleStarHullForm.Generate(S);
            Assert.AreEqual(BrittleStarHullForm.PartKind.Cockpit, parts[0].Kind, "part 0 renders on the painted renderer");
            Assert.AreEqual(1, parts.Count(p => p.Kind == BrittleStarHullForm.PartKind.Ring));
            Assert.AreEqual(BrittleStarHullForm.Points + 1, parts.Count(p => p.Kind == BrittleStarHullForm.PartKind.Flame));
            Assert.AreEqual(parts.Count, parts.Select(p => p.Name).Distinct().Count(), "child parts are found by name");
        }

        [Test]
        public void Form_TheDomainAccentsAreOnSubmeshOne()
        {
            var parts = BrittleStarHullForm.Generate(S);
            Assert.Greater(parts[0].Accent.Count, 0, "the cockpit's canopy and nozzle");
            Assert.Greater(parts.First(p => p.Kind == BrittleStarHullForm.PartKind.Ring).Accent.Count, 0, "the pods' nozzles");
            foreach (var flame in parts.Where(p => p.Kind == BrittleStarHullForm.PartKind.Flame))
                Assert.AreEqual(0, flame.Body.Count, $"{flame.Name} is all accent (domain-coloured)");
            Assert.AreEqual(1, BrittleStarHullForm.AccentSubmesh, "ShipHelper paints the domain onto slot 1");
        }

        [Test]
        public void Form_IsAPentagon_FivePointsAtTheRingRadius()
        {
            for (int i = 0; i < BrittleStarHullForm.Points; i++)
            {
                var p = BrittleStarHullForm.PointPosition(S, i);
                Assert.AreEqual(S.RingRadius, new Vector2(p.x, p.y).magnitude, 1e-4f);
                Assert.AreEqual(72f, Vector2.Angle(new Vector2(p.x, p.y),
                    (Vector2)BrittleStarHullForm.PointPosition(S, i + 1)), 1e-3f);
            }
            Assert.AreEqual(90f, BrittleStarHullForm.PointAngle(0) * Mathf.Rad2Deg, 1e-4f, "point 0 is straight up");
        }

        [Test]
        public void Form_EveryTriangleIsNonDegenerate_AndNormalsAreUnit()
        {
            foreach (var part in BrittleStarHullForm.Generate(S))
            {
                Assert.AreEqual(part.Verts.Count, part.Normals.Count, part.Name);
                Assert.AreEqual(part.Verts.Count, part.Uvs.Count, part.Name);
                foreach (var n in part.Normals) Assert.AreEqual(1f, n.magnitude, 1e-3f, part.Name);
                foreach (var list in new[] { part.Body, part.Accent })
                    for (int t = 0; t < list.Count; t += 3)
                    {
                        Vector3 a = part.Verts[list[t]], b = part.Verts[list[t + 1]], c = part.Verts[list[t + 2]];
                        Assert.Greater(Vector3.Cross(b - a, c - a).sqrMagnitude, 1e-14f, $"{part.Name} tri {t / 3}");
                    }
            }
        }

        [Test]
        public void Form_UnityWinding_FrontFacesAreTheStoredNormals()
        {
            // Unity's front face for indices (i0, i1, i2) is cross(v1 - v0, v2 - v0); it must be the stored normal.
            foreach (var part in BrittleStarHullForm.Generate(S))
                foreach (var list in new[] { part.Body, part.Accent })
                    for (int t = 0; t < list.Count; t += 3)
                    {
                        Vector3 a = part.Verts[list[t]], b = part.Verts[list[t + 1]], c = part.Verts[list[t + 2]];
                        Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), part.Normals[list[t]]), 0f, $"{part.Name} tri {t / 3}");
                    }
        }

        [Test]
        public void Form_EveryPartEnclosesPositiveVolume_SoItsFacesPointOutward()
        {
            // The divergence theorem: with outward front faces, sum(v0 · (v1 × v2)) / 6 is the enclosed volume (> 0).
            // An inside-out piece makes it negative; this is the non-tautological half of the winding check.
            foreach (var part in BrittleStarHullForm.Generate(S))
            {
                double volume = 0;
                foreach (var list in new[] { part.Body, part.Accent })
                    for (int t = 0; t < list.Count; t += 3)
                    {
                        Vector3 a = part.Verts[list[t]], b = part.Verts[list[t + 1]], c = part.Verts[list[t + 2]];
                        volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
                    }
                Assert.Greater(volume, 0.0, $"{part.Name} is inside out");
            }
        }

        [Test]
        public void MorphSet_ExtremesKeepEveryFaceFacingTheSameWay()
        {
            // An orientation flip between the base and an extreme would blend a vertex onto a different corner.
            var baseParts = BrittleStarHullForm.Generate(S);
            foreach (var element in BrittleStarHullForm.MorphElements)
            {
                var extreme = BrittleStarHullForm.Generate(BrittleStarHullForm.Extreme(S, element));
                for (int p = 0; p < baseParts.Count; p++)
                    for (int v = 0; v < baseParts[p].Normals.Count; v++)
                        Assert.Greater(Vector3.Dot(baseParts[p].Normals[v], extreme[p].Normals[v]), 0f,
                            $"{element}: {baseParts[p].Name} vertex {v}");
            }
        }

        [Test]
        public void MorphSet_EveryElementMovesTheHull_AndKeepsTopology()
        {
            var set = BrittleStarHullForm.BakeMorphSet(S);
            for (int e = 0; e < BrittleStarHullForm.MorphElements.Length; e++)
            {
                float maxTravel = 0f;
                foreach (var deltas in set.VertDeltas[e])
                    foreach (var d in deltas) maxTravel = Mathf.Max(maxTravel, d.magnitude);
                Assert.Greater(maxTravel, 0.05f, $"{BrittleStarHullForm.MorphElements[e]} must visibly move the hull");
            }
            Assert.IsTrue(set.PartMorphs[0], "Mass grows the cockpit");
            Assert.IsTrue(set.PartMorphs[1], "Charge, Space and Time reshape the ring");
        }

        [Test]
        public void MorphSet_TimeCurvesTheRingIntoACircle()
        {
            var round = BrittleStarHullForm.Extreme(S, Element.Time);
            for (int side = 0; side < BrittleStarHullForm.Points; side++)
            {
                var mid = BrittleStarHullForm.RingEdgePoint(round, side, 0.5f);
                Assert.AreEqual(S.RingRadius, new Vector2(mid.x, mid.y).magnitude, 1e-3f, "on the circle at Time 10");
                var straight = BrittleStarHullForm.RingEdgePoint(S, side, 0.5f);
                Assert.Less(new Vector2(straight.x, straight.y).magnitude, S.RingRadius - 0.1f, "a pentagon's side at rest");
            }
        }

        [Test]
        public void MorphSet_BoundsCoverEveryWeightCombination()
        {
            var set = BrittleStarHullForm.BakeMorphSet(S);
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var w = new float[4];
            for (int mask = 0; mask < 16; mask++)
            {
                for (int e = 0; e < 4; e++) w[e] = (mask >> e & 1) == 1 ? 1f : 0f;
                for (int p = 0; p < set.BaseParts.Count; p++)
                {
                    BrittleStarHullForm.BlendPart(set, p, w, verts, normals);
                    foreach (var v in verts)
                    {
                        Assert.GreaterOrEqual(v.x, set.BoundsMin[p].x - 1e-4f);
                        Assert.GreaterOrEqual(v.y, set.BoundsMin[p].y - 1e-4f);
                        Assert.GreaterOrEqual(v.z, set.BoundsMin[p].z - 1e-4f);
                        Assert.LessOrEqual(v.x, set.BoundsMax[p].x + 1e-4f);
                        Assert.LessOrEqual(v.y, set.BoundsMax[p].y + 1e-4f);
                        Assert.LessOrEqual(v.z, set.BoundsMax[p].z + 1e-4f);
                    }
                }
            }
        }

        [Test]
        public void Muzzles_SpaceLengthensTheBarrels()
        {
            var far = BrittleStarHullForm.Extreme(S, Element.Space);
            for (int i = -1; i < BrittleStarHullForm.Points; i++)
                Assert.Greater(BrittleStarHullForm.MuzzlePosition(far, i).z, BrittleStarHullForm.MuzzlePosition(S, i).z);
        }

        // ------------------------------------------------------------------ the ring's life (the class document)

        [Test]
        public void Ring_StartsMounted_WithNoCharge()
        {
            var ring = new BrittleStarRules.RingState();
            Assert.IsTrue(ring.Mounted);
            Assert.AreEqual(0, ring.Charges);
            Assert.IsTrue(ring.ModesAvailable);
        }

        [Test]
        public void Ring_LaunchWithoutACharge_LeavesTheHullRinglessAndModeless_UntilACrystal()
        {
            var ring = new BrittleStarRules.RingState();
            Assert.IsTrue(ring.TryLaunch(RebuildSeconds));
            Assert.IsFalse(ring.Mounted);
            Assert.IsFalse(ring.Rebuilding, "no charge: no rebuild");
            Assert.IsFalse(ring.ModesAvailable, "'loses the access to its 3 modes entirely'");
            ring.Tick(10f);
            Assert.IsFalse(ring.Mounted, "time alone never rebuilds");
            Assert.IsFalse(ring.TryLaunch(RebuildSeconds), "no ring, nothing to throw");

            Assert.AreEqual(BrittleStarRules.RingState.CrystalOutcome.Rebuilt, ring.CollectCrystal(RebuildSeconds, OverdriveSeconds));
            Assert.IsFalse(ring.Mounted, "built over rebuildSeconds, not popped");
            Assert.IsTrue(ring.Tick(RebuildSeconds + 0.01f), "the frame the rebuild completes");
            Assert.IsTrue(ring.Mounted);
            Assert.AreEqual(0, ring.Charges, "the crystal was spent on the ring");
        }

        [Test]
        public void Ring_LaunchWithACharge_RebuildsAtOnce_AndTwoRingsCanGoInRapidSuccession()
        {
            var ring = new BrittleStarRules.RingState();
            ring.CollectCrystal(RebuildSeconds, OverdriveSeconds);
            Assert.AreEqual(1, ring.Charges);
            Assert.IsTrue(ring.TryLaunch(RebuildSeconds));
            Assert.IsTrue(ring.Rebuilding, "'it will immediately rebuild its support ring'");
            Assert.AreEqual(0, ring.Charges);
            ring.Tick(RebuildSeconds + 0.01f);
            Assert.IsTrue(ring.TryLaunch(RebuildSeconds), "'no cooldown, it only requires the existence of a support ring'");
            Assert.IsFalse(ring.Rebuilding, "the second ring had no charge behind it");
        }

        [Test]
        public void Ring_OnlyOneChargeIsHeld_ASecondCrystalIsOverdrive()
        {
            var ring = new BrittleStarRules.RingState();
            Assert.AreEqual(BrittleStarRules.RingState.CrystalOutcome.Stored, ring.CollectCrystal(RebuildSeconds, OverdriveSeconds));
            Assert.AreEqual(BrittleStarRules.RingState.CrystalOutcome.Overdrive, ring.CollectCrystal(RebuildSeconds, OverdriveSeconds));
            Assert.AreEqual(1, ring.Charges, "'only one charge can be held at a time'");
            Assert.IsTrue(ring.Overdrive);
            ring.Tick(OverdriveSeconds * 0.5f);
            Assert.IsTrue(ring.Overdrive);
            ring.Tick(OverdriveSeconds);
            Assert.IsFalse(ring.Overdrive, "'for a short time'");
        }

        [Test]
        public void Modes_OverdriveTurnsAllThreeOn_AndNoRingTurnsAllOff()
        {
            var ring = new BrittleStarRules.RingState();
            var sel = BrittleStarRules.Mode.Speed;
            Assert.IsTrue(BrittleStarRules.IsOn(BrittleStarRules.Mode.Speed, sel, ring));
            Assert.IsFalse(BrittleStarRules.IsOn(BrittleStarRules.Mode.Creation, sel, ring));

            ring.CollectCrystal(RebuildSeconds, OverdriveSeconds);
            ring.CollectCrystal(RebuildSeconds, OverdriveSeconds);
            foreach (var m in new[] { BrittleStarRules.Mode.Speed, BrittleStarRules.Mode.Creation, BrittleStarRules.Mode.Destruction })
                Assert.IsTrue(BrittleStarRules.IsOn(m, BrittleStarRules.Mode.Cruise, ring), $"{m} in Overdrive");

            var ringless = new BrittleStarRules.RingState();
            ringless.TryLaunch(RebuildSeconds);
            Assert.IsFalse(BrittleStarRules.IsOn(BrittleStarRules.Mode.Speed, sel, ringless));
            Assert.IsFalse(BrittleStarRules.IsOn(BrittleStarRules.Mode.Cruise, BrittleStarRules.Mode.Cruise, ring), "Cruise is no mode");
        }

        [Test]
        public void Modes_PressingTheSelectedModeReturnsToCruise()
        {
            Assert.AreEqual(BrittleStarRules.Mode.Speed, BrittleStarRules.Press(BrittleStarRules.Mode.Cruise, BrittleStarRules.Mode.Speed));
            Assert.AreEqual(BrittleStarRules.Mode.Creation, BrittleStarRules.Press(BrittleStarRules.Mode.Speed, BrittleStarRules.Mode.Creation));
            Assert.AreEqual(BrittleStarRules.Mode.Cruise, BrittleStarRules.Press(BrittleStarRules.Mode.Creation, BrittleStarRules.Mode.Creation));
        }

        // ------------------------------------------------------------------ aim

        [Test]
        public void Aim_InsideTheDeadzoneAimsNowhere()
        {
            Assert.IsNull(BrittleStarRules.AimAngle(new Vector2(0.1f, 0.1f), 0.25f));
            Assert.IsNotNull(BrittleStarRules.AimAngle(new Vector2(0f, 0.5f), 0.25f));
            Assert.AreEqual(0f, BrittleStarRules.Spread01(new Vector2(0.25f, 0f), 0.25f), 1e-5f);
            Assert.AreEqual(1f, BrittleStarRules.Spread01(new Vector2(1f, 0f), 0.25f), 1e-5f);
        }

        [Test]
        public void Aim_TheRingTurnsTheNearestTurretOntoTheAim_TheShortWayRound()
        {
            for (float aimDeg = -180f; aimDeg < 180f; aimDeg += 7f)
                for (float phaseDeg = -400f; phaseDeg < 400f; phaseDeg += 33f)
                {
                    float aim = aimDeg * Mathf.Deg2Rad, phase = phaseDeg * Mathf.Deg2Rad;
                    int point = BrittleStarRules.NearestPoint(aim, phase);
                    float target = BrittleStarRules.PhaseToAim(aim, point, phase);
                    Assert.LessOrEqual(Mathf.Abs(target - phase) * Mathf.Rad2Deg, 36f + 1e-3f,
                        "the nearest of five points is never more than 36° away");
                    float landed = (BrittleStarHullForm.PointAngle(point) + target) * Mathf.Rad2Deg;
                    Assert.AreEqual(0f, Mathf.DeltaAngle(landed, aimDeg), 1e-3f);
                }
        }

        [Test]
        public void Spread_IsDeterministic_AndStaysInsideTheCone()
        {
            var fwd = Vector3.forward;
            for (uint s = 0; s < 500; s++)
            {
                var a = BrittleStarRules.Deflect(fwd, Vector3.up, 12f, s);
                Assert.AreEqual(a, BrittleStarRules.Deflect(fwd, Vector3.up, 12f, s), "peers agreeing on the serial agree on the round");
                Assert.LessOrEqual(Vector3.Angle(fwd, a), 12f + 1e-3f);
                Assert.AreEqual(1f, a.magnitude, 1e-4f);
            }
            Assert.AreEqual(fwd, BrittleStarRules.Deflect(fwd, Vector3.up, 0f, 7u), "no spread, no deflection");
            Assert.AreNotEqual(BrittleStarRules.Deflect(fwd, Vector3.up, 12f, 1u), BrittleStarRules.Deflect(fwd, Vector3.up, 12f, 2u));
        }

        [Test]
        public void Arms_StickIn_MeetAheadAtTheFocus_StickOut_SplayOutward()
        {
            var fwd = Vector3.forward;
            var outDir = Vector3.up;
            const float r = 6f, focus = 120f, splay = 25f;

            // Stick IN: the arm from a muzzle r out crosses the nose axis exactly `focus` ahead (a crosshair).
            var d = BrittleStarRules.ArmDirection(fwd, outDir, r, 0f, focus, splay);
            float t = focus / d.z;
            Assert.AreEqual(0f, r + d.y * t, 1e-3f, "converges onto the axis");
            Assert.Less(Vector3.Dot(d, outDir), 0f, "angled inward");

            // Stick OUT: splayed the full angle outward.
            var o = BrittleStarRules.ArmDirection(fwd, outDir, r, 1f, focus, splay);
            Assert.AreEqual(splay, Vector3.Angle(fwd, o), 1e-3f);
            Assert.Greater(Vector3.Dot(o, outDir), 0f, "angled outward");

            // In between: the outward angle rises steadily with how far the stick is pushed.
            float last = -999f;
            for (int i = 0; i <= 10; i++)
            {
                var a = BrittleStarRules.ArmDirection(fwd, outDir, r, i / 10f, focus, splay);
                float signed = Vector3.Angle(fwd, a) * Mathf.Sign(Vector3.Dot(a, outDir));
                Assert.Greater(signed, last);
                Assert.AreEqual(1f, a.magnitude, 1e-4f);
                last = signed;
            }
        }

        [Test]
        public void ModeSpeed_CreationIsTheSlowest_SpeedTheFastest_OverdriveGetsBoth()
        {
            const float speedX = 6f, creationX = 0.5f;
            float cruise = BrittleStarRules.ModeSpeed(0f, 0f, speedX, creationX);
            float speed = BrittleStarRules.ModeSpeed(1f, 0f, speedX, creationX);
            float creation = BrittleStarRules.ModeSpeed(0f, 1f, speedX, creationX);
            Assert.AreEqual(1f, cruise, 1e-5f, "cruise and Destruction Mode fly at the plain cruise");
            Assert.AreEqual(6f, speed, 1e-5f, "Speed Mode is x6");
            Assert.AreEqual(0.5f, creation, 1e-5f);
            Assert.Less(creation, cruise, "Creation Mode is the slowest");
            Assert.AreEqual(3f, BrittleStarRules.ModeSpeed(1f, 1f, speedX, creationX), 1e-5f, "Overdrive: both");
            Assert.AreEqual(Mathf.Lerp(1f, 0.5f, 0.4f), BrittleStarRules.ModeSpeed(0f, 0.4f, speedX, creationX), 1e-5f,
                "eases between");
        }

        [Test]
        public void RingExplosion_IsADiskAsThickAsTheRing_NotASphere()
        {
            var n = Vector3.forward;
            const float half = 0.22f, radius = 392f;
            // In the plane, out to the nucleus's edge: reached.
            Assert.IsTrue(BrittleStarRules.DiskReaches(new Vector3(390f, 0f, 0f), n, half, radius, 0f, 0f));
            Assert.IsTrue(BrittleStarRules.DiskReaches(new Vector3(0f, -200f, 0.2f), n, half, radius, 0f, 0f));
            // Past the edge: not reached — unless the body's own extent across the normal crosses it.
            Assert.IsFalse(BrittleStarRules.DiskReaches(new Vector3(400f, 0f, 0f), n, half, radius, 0f, 0f));
            Assert.IsTrue(BrittleStarRules.DiskReaches(new Vector3(400f, 0f, 0f), n, half, radius, 0f, 10f));
            // A short way off the plane — well inside what a sphere would reach — not reached ...
            Assert.IsFalse(BrittleStarRules.DiskReaches(new Vector3(10f, 0f, 3f), n, half, radius, 0f, 0f));
            // ... unless the body is thick enough along the normal to straddle the disk.
            Assert.IsTrue(BrittleStarRules.DiskReaches(new Vector3(10f, 0f, 3f), n, half, radius, 3f, 0f));
            // The disk lies across whatever normal the ring had when it blew.
            var tilted = new Vector3(1f, 1f, 0f).normalized;
            Assert.IsTrue(BrittleStarRules.DiskReaches(new Vector3(0f, 0f, 300f), tilted, half, radius, 0f, 0f));
            Assert.IsFalse(BrittleStarRules.DiskReaches(tilted * 5f, tilted, half, radius, 0f, 0f));
        }

        [Test]
        public void Range_IsSpeedTimesTwoTOverPi()
        {
            Assert.AreEqual(700f * 0.6f / Mathf.PI, BrittleStarRules.RangeFor(700f, 0.3f), 1e-3f);
        }
    }
}

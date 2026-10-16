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
    /// winding, bounds), the pure rules (the ring's life cycle clause by clause from the class document, mode
    /// selection and the aim math), the ring's spare stock and the guns' heat, and Charge Mode's
    /// whip cords (<see cref="BrittleStarCord"/>).
    /// </summary>
    [TestFixture]
    public class BrittleStarTests
    {
        static readonly BrittleStarHullForm.Settings S = BrittleStarHullForm.Defaults;
        const float RebuildSeconds = 0.5f;
        
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

        // ------------------------------------------------------------------ the ring stock (Garrett, 2026-10-16)

        const int MaxSpares = 2;

        [Test]
        public void Ring_StartsMounted_WithNoSpare_AndCannotBeThrown()
        {
            var ring = new BrittleStarRules.RingState();
            Assert.IsTrue(ring.Mounted);
            Assert.AreEqual(0, ring.Spares);
            Assert.IsTrue(ring.ModesAvailable);
            Assert.IsFalse(ring.CanLaunch, "'only let them shoot the ring if they have a spare one'");
            Assert.IsFalse(ring.TryLaunch(RebuildSeconds));
            Assert.IsTrue(ring.Mounted, "a refused throw leaves the ring where it is");
        }

        [Test]
        public void Ring_OneSparePerCrystal_UpToTwo()
        {
            var ring = new BrittleStarRules.RingState();
            Assert.IsTrue(ring.CollectCrystal(MaxSpares));
            Assert.IsTrue(ring.CollectCrystal(MaxSpares));
            Assert.AreEqual(2, ring.Spares, "'they can store two extra rings'");
            Assert.IsFalse(ring.CollectCrystal(MaxSpares), "a full stock takes nothing more");
            Assert.AreEqual(2, ring.Spares);
        }

        [Test]
        public void Ring_AThrowSpendsASpare_AndTheMountedRingRegrowsOnItsOwn()
        {
            var ring = new BrittleStarRules.RingState();
            ring.CollectCrystal(MaxSpares);
            ring.CollectCrystal(MaxSpares);
            Assert.IsTrue(ring.TryLaunch(RebuildSeconds));
            Assert.AreEqual(1, ring.Spares);
            Assert.IsFalse(ring.Mounted);
            Assert.IsTrue(ring.Rebuilding, "'a spare one that automatically regrows'");
            Assert.IsFalse(ring.ModesAvailable, "no modes while it regrows");
            Assert.IsFalse(ring.CanLaunch, "nothing to throw while it regrows");
            Assert.AreEqual(0.5f, ring.Presence01(RebuildSeconds * 2f), 1e-4f);
            Assert.IsTrue(ring.Tick(RebuildSeconds + 0.01f), "the frame the regrowth completes");
            Assert.IsTrue(ring.Mounted);
            Assert.IsTrue(ring.TryLaunch(RebuildSeconds), "the second spare");
            ring.Tick(RebuildSeconds + 0.01f);
            Assert.IsTrue(ring.Mounted, "it regrows even from the last spare - the hull is never ringless");
            Assert.IsFalse(ring.TryLaunch(RebuildSeconds), "but with no spare left it cannot be thrown");
        }

        // ------------------------------------------------------------------ heat (Garrett, 2026-10-16)

        [Test]
        public void Heat_RisesWithRoundsTimesGuns_OverheatsAtFull_RecoversOnlyBelowTheMark()
        {
            var one = new BrittleStarRules.HeatState();
            var five = new BrittleStarRules.HeatState();
            one.AddRounds(1, 0.01f);
            five.AddRounds(5, 0.01f);
            Assert.AreEqual(5f * one.Heat01, five.Heat01, 1e-5f, "heat is proportional to the guns firing");

            var heat = new BrittleStarRules.HeatState();
            bool tripped = false;
            for (int i = 0; i < 200 && !tripped; i++) tripped = heat.AddRounds(5, 0.0012f);
            Assert.IsTrue(tripped && heat.Overheated);
            Assert.AreEqual(1f, heat.Heat01, 1e-5f);
            Assert.IsFalse(heat.AddRounds(5, 0.0012f), "overheats once, not on every round after");
            Assert.IsFalse(heat.Cool(0.12f, 0.35f, 1f), "cooling a little is not recovering");
            Assert.IsTrue(heat.Overheated);
            Assert.IsTrue(heat.Cool(0.12f, 0.35f, 5f), "the frame it falls to the recovery mark");
            Assert.IsFalse(heat.Overheated);
        }

        /// <summary>Seconds of the five guns at a full pull (450 rounds/s, the shipped heat per round and cooling) until
        /// they overheat, and then seconds until they recover, at <paramref name="capacity"/>.</summary>
        static void Burst(float capacity, out float toOverheat, out float lockout)
        {
            const float dt = 1f / 450f;
            var heat = new BrittleStarRules.HeatState();
            toOverheat = 0f;
            while (!heat.Overheated && toOverheat < 60f)
            {
                heat.AddRounds(1, 0.0012f, capacity);
                heat.Cool(0.12f, 0.35f, dt, capacity);
                toOverheat += dt;
            }
            lockout = 0f;
            while (heat.Overheated && lockout < 60f)
            {
                heat.Cool(0.12f, 0.35f, dt, capacity);
                lockout += dt;
            }
        }

        [Test]
        public void Heat_SpaceCapacity_OverheatsThreeTimesSlower_SameLockout()
        {
            // Garrett, 2026-10-16: "overheat 3x slower at max… have the maximum heat scale with space" (heatCapacity x3).
            Burst(1f, out float baseRun, out float baseLockout);
            Burst(3f, out float maxRun, out float maxLockout);
            Assert.AreEqual(1f / (450f * 0.0012f - 0.12f), baseRun, 0.05f, "Space 0: ~2.4 s");
            Assert.AreEqual(3f * baseRun, maxRun, 0.05f, "Space 10: three times as long");
            Assert.AreEqual(baseLockout, maxLockout, 0.05f, "the lockout is the same at any Space");
            Assert.AreEqual(0.65f / 0.12f, baseLockout, 0.05f);

            var full = new BrittleStarRules.HeatState();
            full.AddRounds(1000, 0.01f, 3f);
            Assert.AreEqual(1f, full.Heat01, 1e-5f, "Heat01 is the share of the CURRENT capacity (what the HUD draws)");
        }

        [Test]
        public void Heat_TheCordsCoolTheGunsFaster()
        {
            var plain = new BrittleStarRules.HeatState();
            var charged = new BrittleStarRules.HeatState();
            plain.AddRounds(1000, 0.001f);
            charged.AddRounds(1000, 0.001f);
            float seconds = 0f;
            while (plain.Overheated) { plain.Cool(0.12f, 0.35f, 0.1f); seconds += 0.1f; }
            float chargedSeconds = 0f;
            while (charged.Overheated) { charged.Cool(0.12f + 0.6f, 0.35f, 0.1f); chargedSeconds += 0.1f; }
            Assert.Less(chargedSeconds * 4f, seconds, "'using charge lasers cools the guns'");
        }

        // ------------------------------------------------------------------ modes

        [Test]
        public void Modes_OneAtATime_TheRingMustBeMounted_GunsPerMode()
        {
            var ring = new BrittleStarRules.RingState();
            foreach (var sel in new[] { BrittleStarRules.Mode.Speed, BrittleStarRules.Mode.Creation,
                                        BrittleStarRules.Mode.Destruction, BrittleStarRules.Mode.Charge })
                foreach (var m in new[] { BrittleStarRules.Mode.Speed, BrittleStarRules.Mode.Creation,
                                          BrittleStarRules.Mode.Destruction, BrittleStarRules.Mode.Charge })
                    Assert.AreEqual(sel == m, BrittleStarRules.IsOn(m, sel, ring), $"{m} with {sel} selected");
            Assert.IsFalse(BrittleStarRules.IsOn(BrittleStarRules.Mode.Cruise, BrittleStarRules.Mode.Cruise, ring), "Cruise is no mode");

            ring.CollectCrystal(MaxSpares);
            ring.TryLaunch(RebuildSeconds);
            Assert.IsFalse(BrittleStarRules.IsOn(BrittleStarRules.Mode.Speed, BrittleStarRules.Mode.Speed, ring), "regrowing: no modes");

            Assert.AreEqual(5, BrittleStarRules.GunsFor(BrittleStarRules.Mode.Destruction), "Space Mode: all five guns");
            Assert.AreEqual(0, BrittleStarRules.GunsFor(BrittleStarRules.Mode.Charge), "Charge Mode: the trigger works the cords");
            foreach (var m in new[] { BrittleStarRules.Mode.Cruise, BrittleStarRules.Mode.Speed, BrittleStarRules.Mode.Creation })
                Assert.AreEqual(1, BrittleStarRules.GunsFor(m), $"{m}: the one aimed turret");
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
        public void ModeSpeed_CreationIsTheSlowest_SpeedTheFastest()
        {
            const float speedX = 6f, creationX = 0.5f;
            float cruise = BrittleStarRules.ModeSpeed(0f, 0f, speedX, creationX);
            float speed = BrittleStarRules.ModeSpeed(1f, 0f, speedX, creationX);
            float creation = BrittleStarRules.ModeSpeed(0f, 1f, speedX, creationX);
            Assert.AreEqual(1f, cruise, 1e-5f, "cruise and Destruction Mode fly at the plain cruise");
            Assert.AreEqual(6f, speed, 1e-5f, "Speed Mode is x6");
            Assert.AreEqual(0.5f, creation, 1e-5f);
            Assert.Less(creation, cruise, "Creation Mode is the slowest");
            Assert.AreEqual(3f, BrittleStarRules.ModeSpeed(1f, 1f, speedX, creationX), 1e-5f, "a hand-over between the two: both");
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

        // ------------------------------------------------------------------ Charge Mode: the five whip cords

        // The shipped dials (BrittleStarConfigSO's Charge Mode block).
        static readonly BrittleStarCord.Settings Cord = new()
        {
            LooseWave = 144f, RigidWave = 2500f, LooseDamping = 0.4f, RigidDamping = 14f, Pose = 1600f,
        };
        /// <summary>cordMaxRigidity: how rigid a FULL pull makes the cords (Garrett, 2026-10-16: "not completely rigid at
        /// full press… 30% of the current max elasticity").</summary>
        const float FullPullRigidity = 0.7f;
        const int CordLinks = 16;
        const float SaberLength = 80f, WhipLength = 120f;

        /// <summary>A ring flick: the muzzle sits 6 u out on a ring that turns 72° at 540 °/s after 0.2 s, its cord posed
        /// splayed 25° outward. Calls <paramref name="each"/> after every step; returns the largest distance the tip strays
        /// from its place on the pose.</summary>
        static float Flick(float dt, float rigidity, out BrittleStarCord cord, float seconds = 5f,
                           System.Action<float, BrittleStarCord> each = null)
        {
            cord = new BrittleStarCord(CordLinks);
            float length = Mathf.Lerp(WhipLength, SaberLength, rigidity);
            float phase = 0f, target = 72f * Mathf.Deg2Rad, worst = 0f;
            for (float t = 0f; t < seconds; t += dt)
            {
                if (t > 0.2f) phase = Mathf.Min(target, phase + 540f * Mathf.Deg2Rad * dt);
                var outward = new Vector3(Mathf.Cos(phase), Mathf.Sin(phase), 0f);
                var dir = (Vector3.forward * Mathf.Cos(25f * Mathf.Deg2Rad) + outward * Mathf.Sin(25f * Mathf.Deg2Rad)).normalized;
                cord.Step(outward * 6f, dir, length, rigidity, Cord, dt);
                worst = Mathf.Max(worst, (cord.Tip - (outward * 6f + dir * length)).magnitude);
                each?.Invoke(t, cord);
            }
            return worst;
        }

        [Test]
        public void Cord_UnseededIsLaidStraightAlongItsPose()
        {
            var cord = new BrittleStarCord(CordLinks);
            Assert.IsFalse(cord.Seeded);
            cord.Step(new Vector3(6f, 0f, 0f), Vector3.forward, WhipLength, 0f, Cord, 1f / 60f);
            Assert.IsTrue(cord.Seeded);
            Assert.AreEqual(CordLinks + 1, cord.Count);
            for (int i = 0; i < cord.Count; i++)
                Assert.AreEqual(0f, (cord.Points[i] - new Vector3(6f, 0f, WhipLength * i / CordLinks)).magnitude, 1e-4f);
            cord.Clear();
            Assert.IsFalse(cord.Seeded, "a cleared cord is laid afresh, never resumed from where it was drawn back");
        }

        [Test]
        public void Cord_RidesAlongWithTheHull_AtAnySpeed()
        {
            // Speed Mode at x6: 270 u/s straight ahead for two seconds, whip and saber alike.
            foreach (float rigidity in new[] { 0f, 1f })
            {
                var cord = new BrittleStarCord(CordLinks);
                var anchor = new Vector3(6f, 0f, 0f);
                for (int f = 0; f < 120; f++)
                {
                    anchor += Vector3.forward * 270f / 60f;
                    cord.Step(anchor, Vector3.forward, WhipLength, rigidity, Cord, 1f / 60f);
                }
                Assert.AreEqual(0f, (cord.Tip - (anchor + Vector3.forward * WhipLength)).magnitude, 1e-3f);
            }
        }

        [Test]
        public void Cord_Whip_APulseTravelsDownTheCord_AtLowSpeed_AndRingsOn()
        {
            // When does each link start to swing after the flick at 0.2 s? A wave reaches them in order, slowly.
            float[] first = new float[CordLinks];
            for (int i = 0; i < first.Length; i++) first[i] = -1f;
            float lateStray = 0f;
            Flick(1f / 240f, 0f, out _, 5f, (t, c) =>
            {
                for (int i = 0; i < CordLinks; i++)
                    if (first[i] < 0f && t > 0.2f && c.LinkSwing(i) > 0.5f) first[i] = t;
                if (t > 2.5f) lateStray = Mathf.Max(lateStray, c.LinkSwing(CordLinks - 1));
            });
            Assert.Greater(first[4], 0f);
            Assert.Greater(first[8], first[4], "the wave reaches the middle after the root");
            Assert.Greater(first[CordLinks - 1], first[8], "and the tip after the middle");
            Assert.Greater(first[CordLinks - 1] - 0.2f, 0.6f, "low speed: the tip feels the flick more than half a second later");
            Assert.Greater(lateStray, 0.1f, "high inertia: the whip is still swinging seconds after the flick");
        }

        [Test]
        public void Cord_Saber_HoldsStraight_AndSettlesFast()
        {
            float whip = Flick(1f / 60f, 0f, out _);
            float saber = Flick(1f / 60f, 1f, out var rigid);
            Assert.Less(saber * 3f, whip, "pulled, the cord bends far less than the whip");
            Assert.Less(saber, 15f);
            for (int i = 0; i < CordLinks; i++)
                Assert.Greater(Vector3.Dot(rigid.LinkDirection(i), rigid.LinkDirection(0)), 0.9999f, $"link {i}: a straight saber");
        }

        [Test]
        public void Cord_FullPull_KeepsSomeGive_ButHoldsItsLine()
        {
            // A full pull stops at cordMaxRigidity: a flick still bends the saber visibly more than the fully rigid dials
            // would, yet far less than the whip, and the cord settles back onto its line.
            float whip = Flick(1f / 60f, 0f, out _);
            float rigid = Flick(1f / 60f, 1f, out _);
            float pulled = Flick(1f / 60f, FullPullRigidity, out var cord, 6f);
            Assert.Greater(pulled, rigid * 1.2f, "30% of the give stays: it bends more than a fully rigid cord");
            Assert.Less(pulled * 2f, whip, "but it is a saber, not a whip");
            for (int i = 0; i < CordLinks; i++)
                Assert.Greater(Vector3.Dot(cord.LinkDirection(i), cord.LinkDirection(0)), 0.999f, $"link {i}: settled straight");
        }

        [Test]
        public void Cord_NeverStretches_ItsLengthIsItsLinks()
        {
            // The worst case the hull can make: flicks of the ring while the hull yaws hard at Speed Mode's pace, whip.
            var cord = new BrittleStarCord(CordLinks);
            float seg = WhipLength / CordLinks, yaw = 0f, phase = 0f;
            var pos = Vector3.zero;
            for (int f = 0; f < 240; f++)
            {
                const float dt = 1f / 30f;
                yaw += 107f * Mathf.Deg2Rad * dt;
                phase += 540f * Mathf.Deg2Rad * dt * (f % 40 < 8 ? 1f : 0f);
                var fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                var right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
                var outward = right * Mathf.Cos(phase) + Vector3.up * Mathf.Sin(phase);
                pos += fwd * 270f * dt;
                cord.Step(pos + outward * 6f, (fwd + outward * 0.4f).normalized, WhipLength, 0f, Cord, dt);
                for (int i = 1; i < cord.Count; i++)
                    Assert.AreEqual(seg, (cord.Points[i] - cord.Points[i - 1]).magnitude, 1e-3f,
                        $"frame {f}, link {i}: a cord neither stretches nor shrinks (low elasticity)");
            }
        }

        [Test]
        public void Cord_StaysBounded_OverAMinuteOfFlying()
        {
            var cord = new BrittleStarCord(CordLinks);
            float yaw = 0f, phase = 0f, worst = 0f;
            var pos = Vector3.zero;
            for (float t = 0f; t < 60f; t += 1f / 30f)
            {
                phase += Mathf.Sin(t * 1.7f) * 4f / 30f;
                yaw += Mathf.Sin(t * 0.9f) * 1.5f / 30f;
                var fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                var right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
                var outward = right * Mathf.Cos(phase) + Vector3.up * Mathf.Sin(phase);
                pos += fwd * 1.5f;
                cord.Step(pos + outward * 6f, (fwd + outward * 0.42f).normalized, WhipLength, 0f, Cord, 1f / 30f);
                for (int i = 0; i < CordLinks; i++) worst = Mathf.Max(worst, cord.LinkSwing(i));
            }
            Assert.Less(worst, 20f, "a whip flown for a minute never winds itself up (rad/s)");
        }

        [Test]
        public void Cord_WhipsAlikeAt30And144Fps()
        {
            float at30 = Flick(1f / 30f, 0f, out _), at60 = Flick(1f / 60f, 0f, out _), at144 = Flick(1f / 144f, 0f, out _);
            Assert.AreEqual(at144, at30, at144 * 0.3f, "sub-stepped: a 30 fps peer sees the same whip");
            Assert.AreEqual(at144, at60, at144 * 0.3f);
        }
    }
}

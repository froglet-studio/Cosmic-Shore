using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The scarab-wing dais contract (SCARAB.md §5). The rosette is authored geometry that ten
    /// separate peers each rebuild from scratch, and whose prisms are stated rather than grown —
    /// so the properties worth pinning are the ones a silent regression would eat: the mirror
    /// symmetry that makes it read as wing PAIRS, the monotone size gradient along a wing, the
    /// tier cycle, the shield fit that keeps a tier from becoming a size change, the sun core's
    /// apparent size, and the ring clearance that keeps the dais out of the switch's own mouth.
    /// </summary>
    public class ScarabWingDaisTests
    {
        static readonly Vector3 Center = new(120f, -40f, 7f);
        static readonly Vector3 Axis = new(0.3f, -0.6f, 0.74f);
        const float RingRadius = 20f;
        const float Tol = 1e-3f;

        static (Vector3 axis, Vector3 u, Vector3 v) Frame()
        {
            Vector3 axis = Axis.normalized;
            Vector3 u = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;
            return (axis, u, v);
        }

        static List<ScarabWingDais.Element> Build(ScarabWingDaisSettings s, float ring = RingRadius)
        {
            var (axis, u, v) = Frame();
            var list = new List<ScarabWingDais.Element>();
            ScarabWingDais.Generate(s, Center, axis, u, v, ring, list);
            return list;
        }

        static ScarabWingDaisSettings Flat()
        {
            var s = ScarabWingDaisSettings.Default;
            s.DishRise = 0f;          // most geometry assertions are planar; the dish gets its own test
            return s;
        }

        [Test]
        public void Generate_LaysExactlyTheAdvertisedPrismCount()
        {
            var s = ScarabWingDaisSettings.Default;
            var built = Build(s);
            Assert.AreEqual(s.PrismCount, built.Count,
                "PrismCount is what the budget and the collider-cost note are quoted from — it must " +
                "match what Generate actually emits.");
            Assert.AreEqual(s.PairCount * 2 * s.FeathersPerWing + s.PairCount, built.Count);
        }

        [Test]
        public void Generate_ProducesOneSunCorePerPairAndItIsSuperShieldedAndCubic()
        {
            var s = ScarabWingDaisSettings.Default;
            var suns = Build(s).FindAll(e => e.IsSunCore);

            Assert.AreEqual(s.PairCount, suns.Count, "one sun core per wing pair");
            var seen = new HashSet<int>();
            foreach (var sun in suns)
            {
                Assert.AreEqual(PrismKind.SuperShielded, sun.Kind);
                Assert.IsTrue(seen.Add(sun.Pair), "each pair gets exactly one sun core");
                Assert.AreEqual(sun.Scale.x, sun.Scale.y, Tol, "the sun core is a CUBE");
                Assert.AreEqual(sun.Scale.x, sun.Scale.z, Tol, "the sun core is a CUBE");
            }
        }

        [Test]
        public void SunCore_ApparentSizeIsWhatWasAuthored_NotTheCubeEdge()
        {
            var s = Flat();
            var sun = Build(s).Find(e => e.IsSunCore);

            // The stellation's spike tips sit at the corners of a cube CIRCUMSCRIBING_SCALE x the
            // authored one, so the field states what the player SEES and the cube is derived.
            float apparent = sun.Scale.x * ScarabWingDais.SunApparentFactor;
            Assert.AreEqual(RingRadius * s.SunApparentDiameter, apparent, Tol);
            Assert.AreEqual(StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE,
                            ScarabWingDais.SunApparentFactor, Tol);
        }

        [Test]
        public void ShieldedBlade_IsFittedSoItsOctahedronMatchesThePlainBladeEnvelope()
        {
            // The fit is derived from the shield generator's own constant, never a literal:
            // semi-axes are CIRCUMSCRIBING_SCALE x the box half-extents, so a 1/3 box reaches
            // exactly as far as an unfitted box of the nominal size.
            Assert.AreEqual(1f / OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE,
                            ScarabWingDais.ShieldedFit, 1e-6f);

            float nominal = 12f;
            float shieldReach = (nominal * ScarabWingDais.ShieldedFit) * 0.5f
                                * OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
            Assert.AreEqual(nominal * 0.5f, shieldReach, Tol,
                "a fitted shielded blade must reach exactly as far as the plain blade it stands in for");
        }

        [Test]
        public void Blades_CycleBaseShieldedDangerAlongTheWing()
        {
            var s = Flat();
            var byIndex = new Dictionary<int, PrismKind>();
            foreach (var e in Build(s))
            {
                if (e.IsSunCore) continue;
                if (byIndex.TryGetValue(e.Feather, out var seen))
                    Assert.AreEqual(seen, e.Kind, "every wing wears the same tier at the same blade index");
                else byIndex[e.Feather] = e.Kind;
            }

            for (int i = 0; i < s.FeathersPerWing; i++)
                Assert.AreEqual(ScarabWingDais.KindAt(s, i), byIndex[i]);

            Assert.AreEqual(PrismKind.Plain, byIndex[0]);
            Assert.AreEqual(PrismKind.Shielded, byIndex[1]);
            Assert.AreEqual(PrismKind.Danger, byIndex[2]);
            Assert.AreEqual(PrismKind.Plain, byIndex[3], "the cycle repeats along the wing");
        }

        [Test]
        public void TierCycleOffset_RotatesThePatternWithoutChangingItsMix()
        {
            var s = Flat();
            s.TierCycleOffset = 1;
            Assert.AreEqual(PrismKind.Shielded, ScarabWingDais.KindAt(s, 0));
            Assert.AreEqual(PrismKind.Danger, ScarabWingDais.KindAt(s, 1));
            Assert.AreEqual(PrismKind.Plain, ScarabWingDais.KindAt(s, 2));

            // FeathersPerWing is a multiple of 3, so the mix is exactly one third each whatever
            // the offset — which is the property the collider-budget number is quoted against.
            var counts = new Dictionary<PrismKind, int>();
            foreach (var e in Build(s))
            {
                if (e.IsSunCore) continue;
                counts.TryGetValue(e.Kind, out int c);
                counts[e.Kind] = c + 1;
            }
            int blades = s.PairCount * 2 * s.FeathersPerWing;
            Assert.AreEqual(blades / 3, counts[PrismKind.Plain]);
            Assert.AreEqual(blades / 3, counts[PrismKind.Shielded]);
            Assert.AreEqual(blades / 3, counts[PrismKind.Danger]);
        }

        [Test]
        public void BladeLength_RisesMonotonicallyAlongTheWing()
        {
            var s = Flat();
            var lengths = new Dictionary<int, float>();
            foreach (var e in Build(s))
            {
                if (e.IsSunCore) continue;
                // Undo the shielded fit: the QUESTION is about the envelope the blade occupies,
                // which is the same for every tier by construction.
                float fit = e.Kind == PrismKind.Shielded ? ScarabWingDais.ShieldedFit : 1f;
                lengths[e.Feather] = e.Scale.z / fit;
            }
            for (int i = 1; i < s.FeathersPerWing; i++)
                Assert.Greater(lengths[i], lengths[i - 1],
                    $"blade {i} must be longer than blade {i - 1} — 'units of increasing size' is the motif");

            Assert.Greater(lengths[s.FeathersPerWing - 1] / lengths[0], 1.5f,
                "the gradient has to be visible, not a rounding difference");
        }

        [Test]
        public void Wings_AreExactMirrorsAcrossTheirPairAxis()
        {
            var s = Flat();
            var (axis, u, v) = Frame();
            var built = Build(s);

            int checkedPairs = 0;
            for (int p = 0; p < s.PairCount; p++)
            {
                float pairDeg = p * 360f / s.PairCount * Mathf.Deg2Rad;
                Vector3 mirrorNormal = (-Mathf.Sin(pairDeg) * u + Mathf.Cos(pairDeg) * v).normalized;

                for (int f = 0; f < s.FeathersPerWing; f++)
                {
                    Vector3 a = Find(built, p, +1, f).Position - Center;
                    Vector3 b = Find(built, p, -1, f).Position - Center;
                    // Reflecting one wing's blade in the pair's plane must land on the other's.
                    Vector3 reflected = a - 2f * Vector3.Dot(a, mirrorNormal) * mirrorNormal;
                    Assert.Less((reflected - b).magnitude, 1e-2f,
                        $"pair {p} blade {f} is not a mirror image — the pairing is the whole motif");
                    checkedPairs++;
                }
            }
            Assert.AreEqual(s.PairCount * s.FeathersPerWing, checkedPairs);
            Assert.AreEqual(0f, Vector3.Dot(axis, axis) - 1f, Tol);
        }

        [Test]
        public void Pairs_AreEvenlySpacedAndIdenticalUnderRotation()
        {
            var s = Flat();
            var built = Build(s);
            var (axis, _, _) = Frame();

            float step = 360f / s.PairCount;
            for (int p = 1; p < s.PairCount; p++)
            {
                var rot = Quaternion.AngleAxis(step * p, axis);
                for (int f = 0; f < s.FeathersPerWing; f++)
                {
                    Vector3 expected = Center + rot * (Find(built, 0, +1, f).Position - Center);
                    Assert.Less((Find(built, p, +1, f).Position - expected).magnitude, 1e-2f,
                        $"pair {p} is not pair 0 rotated by {step * p:F0} degrees");
                }
            }
        }

        [Test]
        public void EverythingLiesOutsideTheSwitchRing()
        {
            var s = Flat();
            var (axis, _, _) = Frame();
            foreach (var e in Build(s))
            {
                // Planar distance from the ring's centre, measured in the dais plane.
                Vector3 rel = e.Position - Center;
                float planar = Vector3.ProjectOnPlane(rel, axis).magnitude;
                float halfSpan = 0.5f * Mathf.Max(e.Scale.x, Mathf.Max(e.Scale.y, e.Scale.z));
                Assert.Greater(planar + halfSpan, RingRadius,
                    "nothing in the dais may sit inside the switch's own mouth");
            }
        }

        [Test]
        public void EverythingScalesWithTheRingRadius()
        {
            var s = Flat();
            var a = Build(s, RingRadius);
            var b = Build(s, RingRadius * 2.5f);   // the Mass element's ceiling

            Assert.AreEqual(a.Count, b.Count, "growing the switch must not change the motif");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Less(((b[i].Position - Center) - (a[i].Position - Center) * 2.5f).magnitude, 1e-2f);
                Assert.Less((b[i].Scale - a[i].Scale * 2.5f).magnitude, 1e-2f);
                Assert.AreEqual(a[i].Kind, b[i].Kind);
            }
        }

        [Test]
        public void Dish_LiftsTheRimAlongThePlacementAxisAndLeavesTheHubFlattest()
        {
            var s = ScarabWingDaisSettings.Default;
            Assert.Greater(s.DishRise, 0f, "the shipped motif is a shallow bowl, not a flat decal");

            var (axis, _, _) = Frame();
            float innermost = float.MaxValue, outermost = float.MinValue;
            float liftInner = 0f, liftOuter = 0f;
            foreach (var e in Build(s))
            {
                if (e.IsSunCore) continue;
                Vector3 rel = e.Position - Center;
                float planar = Vector3.ProjectOnPlane(rel, axis).magnitude;
                float lift = Vector3.Dot(rel, axis);
                if (planar < innermost) { innermost = planar; liftInner = lift; }
                if (planar > outermost) { outermost = planar; liftOuter = lift; }
            }
            Assert.Greater(liftOuter, liftInner,
                "the rim rises out of the switch's plane — that is what makes it a dais");
        }

        [Test]
        public void OuterReach_BoundsEveryPrismItAdvertises()
        {
            var s = Flat();
            float reach = ScarabWingDais.OuterReach(s, RingRadius);
            var (axis, _, _) = Frame();

            float maxPlanar = 0f;
            foreach (var e in Build(s))
            {
                float planar = Vector3.ProjectOnPlane(e.Position - Center, axis).magnitude;
                maxPlanar = Mathf.Max(maxPlanar, planar);
            }
            // The reach is the outermost blade's TIP, so every prism CENTRE is inside it.
            Assert.LessOrEqual(maxPlanar, reach + Tol);
            Assert.Greater(reach, RingRadius, "a dais that does not leave the ring is not a dais");
        }

        [Test]
        public void BladesStateSizesThePrismPoolWouldOtherwiseClampAway()
        {
            // The regression this guards: PrismScaleAnimator.SetTargetScale silently clamps into
            // [minScale, maxScale] — (0.5,0.5,0.5)..(40,10,10) on the interactive prism — so a
            // dais laid without AdmitTargetScale is a field of 10-unit stubs with no error.
            var s = Flat();
            bool overCeiling = false, underFloor = false;
            foreach (var e in Build(s))
            {
                if (e.IsSunCore) continue;
                overCeiling |= e.Scale.z > 10f;
                underFloor |= Mathf.Min(e.Scale.x, Mathf.Min(e.Scale.y, e.Scale.z)) < 0.5f;
            }
            Assert.IsTrue(overCeiling, "the long blades must exceed the pool's default axis ceiling");
            Assert.IsTrue(underFloor, "the fitted shielded blades must go under the pool's floor");
        }

        [Test]
        public void Generate_IsDeterministic()
        {
            // Every peer rebuilds the dais locally from a replicated input event; nothing about
            // it is networked, so identical inputs must give bit-identical output.
            var s = ScarabWingDaisSettings.Default;
            var a = Build(s);
            var b = Build(s);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].Rotation, b[i].Rotation);
                Assert.AreEqual(a[i].Scale, b[i].Scale);
                Assert.AreEqual(a[i].Kind, b[i].Kind);
            }
        }

        [Test]
        public void Generate_OrdersOutwardSoTheRosetteBloomsFromTheRing()
        {
            var s = Flat();
            var built = Build(s);
            int lastFeather = -1;
            bool sunsStarted = false;
            foreach (var e in built)
            {
                if (e.IsSunCore) { sunsStarted = true; continue; }
                Assert.IsFalse(sunsStarted, "the sun cores ignite LAST, after every blade");
                Assert.GreaterOrEqual(e.Feather, lastFeather, "blades are laid ring-outward");
                lastFeather = e.Feather;
            }
            Assert.IsTrue(sunsStarted);
        }

        static ScarabWingDais.Element Find(List<ScarabWingDais.Element> built, int pair, int sign, int feather)
        {
            foreach (var e in built)
                if (e.Pair == pair && e.WingSign == sign && e.Feather == feather) return e;
            Assert.Fail($"no blade for pair {pair} wing {sign} index {feather}");
            return default;
        }
    }
}

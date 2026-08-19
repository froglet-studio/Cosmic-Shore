using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The scarab-wing dais contract (SCARAB.md §5.1). The rosette is authored geometry that every
    /// peer rebuilds from scratch and whose prisms are STATED rather than grown, so the properties
    /// worth pinning are the ones a silent regression eats: that nothing overlaps or clips (proved
    /// against the real SHIELD silhouettes, not the boxes), that the run turns ONLY at the
    /// octahedral hinges, that a tier change is not a size change, that the sun core's authored
    /// size means what the tooltip says it means, and that the whole thing scales with the ring.
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

        // ------------------------------------------------------------------
        //  The silhouette a prism actually presents — the shield MESH, not the box. This is the
        //  distinction the first pass got wrong: an octahedron's vertices sit ON THE AXES while a
        //  stella octangula's spikes sit at the CUBE CORNERS, so the two tiers have the same axis
        //  extent and different apparent size.
        // ------------------------------------------------------------------
        static List<Vector2> Silhouette(ScarabWingDais.Element e, Vector3 u, Vector3 v)
        {
            Vector3 fwd = e.Rotation * Vector3.forward;
            Vector2 p = new(Vector3.Dot(e.Position - Center, u), Vector3.Dot(e.Position - Center, v));
            Vector2 d = new Vector2(Vector3.Dot(fwd, u), Vector3.Dot(fwd, v)).normalized;
            Vector2 n = new(-d.y, d.x);
            var poly = new List<Vector2>();
            float hw = e.Scale.x * 0.5f, hl = e.Scale.z * 0.5f;

            if (e.Kind == PrismKind.Shielded)
            {
                float a = hl * OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
                float b = hw * OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
                poly.Add(p + d * a); poly.Add(p + n * b); poly.Add(p - d * a); poly.Add(p - n * b);
            }
            else if (e.Kind == PrismKind.SuperShielded)
            {
                float a = hw * StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
                float r = a * Mathf.Sqrt(2f);
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.PI / 4f;
                    float rr = (i % 2 == 1) ? r : a;
                    poly.Add(p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr);
                }
            }
            else
            {
                poly.Add(p + d * hl + n * hw); poly.Add(p + d * hl - n * hw);
                poly.Add(p - d * hl - n * hw); poly.Add(p - d * hl + n * hw);
            }
            return poly;
        }

        /// <summary>Exact separating-axis test. The dais is planar, so this is conservative for
        /// the 3D structure: disjoint here means disjoint in the world.</summary>
        static bool Separated(List<Vector2> a, List<Vector2> b)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var p = pass == 0 ? a : b;
                for (int i = 0; i < p.Count; i++)
                {
                    Vector2 edge = p[(i + 1) % p.Count] - p[i];
                    Vector2 axis = new(-edge.y, edge.x);
                    if (axis.sqrMagnitude < 1e-12f) continue;
                    axis = axis.normalized;
                    float a0 = float.MaxValue, a1 = float.MinValue, b0 = float.MaxValue, b1 = float.MinValue;
                    foreach (var q in a) { float t = Vector2.Dot(axis, q); a0 = Mathf.Min(a0, t); a1 = Mathf.Max(a1, t); }
                    foreach (var q in b) { float t = Vector2.Dot(axis, q); b0 = Mathf.Min(b0, t); b1 = Mathf.Max(b1, t); }
                    if (a1 < b0 - 1e-5f || b1 < a0 - 1e-5f) return true;
                }
            }
            return false;
        }

        [Test]
        public void TheRosetteHasNoOverlapsAndNoClipping()
        {
            // The headline promise, and the only one that has to be checked against the SHIELD
            // silhouettes: fitting a shielded blade to the plain blade's envelope means the
            // octahedron reaches exactly as far as the box it replaces, so a box-based check
            // would pass a structure whose diamonds were fusing.
            var s = ScarabWingDaisSettings.Default;
            var (_, u, v) = Frame();
            var built = Build(s);
            var sil = new List<List<Vector2>>(built.Count);
            foreach (var e in built) sil.Add(Silhouette(e, u, v));

            for (int i = 0; i < sil.Count; i++)
            for (int j = i + 1; j < sil.Count; j++)
                Assert.IsTrue(Separated(sil[i], sil[j]),
                    $"{built[i].Kind} (pair {built[i].Pair} wing {built[i].WingSign} blade {built[i].Feather}) " +
                    $"overlaps {built[j].Kind} (pair {built[j].Pair} wing {built[j].WingSign} blade {built[j].Feather})");
        }

        [Test]
        public void TheRunTurnsOnlyAtTheOctahedralHinges()
        {
            // A plain blade presents a flat root EDGE, so two of them laid flush are parallel and
            // cannot turn; the shielded blade's root POINT is the only place the curve moves.
            // This is what makes the tier pattern and the shape the same statement.
            var s = ScarabWingDaisSettings.Default;
            var built = Build(s);
            int hinges = 0;
            for (int b = 1; b < s.BladesPerWing; b++)
            {
                var prev = Find(built, 0, +1, b - 1);
                var cur = Find(built, 0, +1, b);
                float deg = Vector3.Angle(prev.Rotation * Vector3.forward, cur.Rotation * Vector3.forward);
                bool touchesHinge = prev.Kind == PrismKind.Shielded || cur.Kind == PrismKind.Shielded;
                if (touchesHinge) { hinges++; Assert.Greater(deg, 1f, $"hinge joint {b - 1}->{b} must actually turn"); }
                // Consecutive boxes share the SAME direction vector by construction, so the only
                // reason this is not exactly 0 is Vector3.Angle's acos losing precision near a unit
                // dot product. Half a degree is still two orders of magnitude under a real hinge.
                else Assert.Less(deg, 0.5f, $"joint {b - 1}->{b} is box-to-box and must stay parallel (got {deg:F3}deg)");
            }
            Assert.Greater(hinges, 0, "a wing with no hinge is a straight line, not a curve");

            float sweep = Vector3.Angle(Find(built, 0, +1, 0).Rotation * Vector3.forward,
                                        Find(built, 0, +1, s.BladesPerWing - 1).Rotation * Vector3.forward);
            Assert.Greater(sweep, 30f, "the wing has to read as a curve, not a nudge");
        }

        [Test]
        public void EveryBladeGrowsAwayFromTheSwitch()
        {
            // The rosette surrounds the switch and points OUT of it. A wing that curled back past
            // tangential would read as growing inward, which is what the first tiled pass did.
            var s = ScarabWingDaisSettings.Default;
            var (axis, _, _) = Frame();
            foreach (var e in Build(s))
            {
                if (e.IsSunCore) continue;
                Vector3 radial = Vector3.ProjectOnPlane(e.Position - Center, axis).normalized;
                Assert.Greater(Vector3.Dot(radial, e.Rotation * Vector3.forward), 0f,
                    $"blade {e.Feather} of pair {e.Pair} wing {e.WingSign} points back toward the switch");
            }
        }

        [Test]
        public void EverySunIsCradledInboardOfItsOwnWings()
        {
            // The sun is not the root the wings sprout from — it sits in their crook, with the
            // pair wrapping around it and growing past.
            var s = ScarabWingDaisSettings.Default;
            var (axis, _, _) = Frame();
            var built = Build(s);
            float Planar(ScarabWingDais.Element e) =>
                Vector3.ProjectOnPlane(e.Position - Center, axis).magnitude;

            foreach (var sun in built.FindAll(e => e.IsSunCore))
            {
                float nearest = float.MaxValue;
                foreach (var b in built)
                    if (!b.IsSunCore && b.Pair == sun.Pair) nearest = Mathf.Min(nearest, Planar(b));
                Assert.Less(Planar(sun), nearest,
                    $"sun {sun.Pair} is not inboard of its own pair's blades — the wings sprout from it");
            }
        }

        [Test]
        public void HingeAspectSetsTheTurnIndependentlyOfTheBladeRamp()
        {
            // A hinge pivots rather than advancing the chain, so its width buys curvature without
            // lengthening the wing. That is the only reason the rosette can be both tightly
            // wrapped around the switch and strongly curved.
            var s = ScarabWingDaisSettings.Default;
            var built = Build(s);
            foreach (var e in built)
            {
                if (e.IsSunCore || e.Kind != PrismKind.Shielded) continue;
                float envelopeWidth = e.Scale.x / ScarabWingDais.ShieldedFit;
                float envelopeLength = e.Scale.z / ScarabWingDais.ShieldedFit;
                Assert.AreEqual(s.HingeAspect, envelopeWidth / envelopeLength, 1e-3f,
                    "a hinge wears its authored aspect, not the blade ramp's");
            }
        }

        [Test]
        public void ShieldedBladesCapBothEndsAndRecurAsHinges()
        {
            var s = ScarabWingDaisSettings.Default;
            Assert.AreEqual(PrismKind.Shielded, ScarabWingDais.KindAt(s, 0), "the curve gets a beginning");
            Assert.AreEqual(PrismKind.Shielded, ScarabWingDais.KindAt(s, s.BladesPerWing - 1), "and an end");
            Assert.AreEqual(PrismKind.Shielded, ScarabWingDais.KindAt(s, s.HingeEvery), "hinges recur");
        }

        [Test]
        public void EverythingElseAlternatesPlainAndDanger()
        {
            var s = ScarabWingDaisSettings.Default;
            PrismKind last = PrismKind.SuperShielded;
            int seen = 0;
            for (int b = 0; b < s.BladesPerWing; b++)
            {
                var k = ScarabWingDais.KindAt(s, b);
                if (k == PrismKind.Shielded) continue;
                Assert.IsTrue(k == PrismKind.Plain || k == PrismKind.Danger, "only three tiers are in the pattern");
                if (seen > 0) Assert.AreNotEqual(last, k, $"blade {b} repeats the previous non-hinge tier");
                last = k; seen++;
            }
            Assert.Greater(seen, 3, "the alternation needs enough blades to read");
        }

        [Test]
        public void ShieldedBladeIsFittedSoItsOctahedronMatchesThePlainEnvelope()
        {
            Assert.AreEqual(1f / OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE,
                            ScarabWingDais.ShieldedFit, 1e-6f);
            float nominal = 12f;
            float reach = nominal * ScarabWingDais.ShieldedFit * 0.5f
                          * OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
            Assert.AreEqual(nominal * 0.5f, reach, Tol,
                "a fitted shielded blade must reach exactly as far as the plain blade it stands in for");
        }

        [Test]
        public void SunCoreApparentSizeIsTheSphereItsSpikesReach_NotItsBoundingBox()
        {
            // The regression this pins: a stella octangula's spikes sit at the CUBE CORNERS, so
            // sizing one by its axis extent understates what the player sees by sqrt(3).
            Assert.AreEqual(StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE * Mathf.Sqrt(3f),
                            ScarabWingDais.SunApparentFactor, 1e-5f);
            Assert.Greater(ScarabWingDais.SunApparentFactor,
                           StellatedOctahedronMeshGenerator.CIRCUMSCRIBING_SCALE,
                           "the spike sphere is strictly larger than the axis extent");

            var s = ScarabWingDaisSettings.Default;
            var sun = Build(s).Find(e => e.IsSunCore);
            Assert.AreEqual(sun.Scale.x, sun.Scale.y, Tol, "the sun core is a CUBE");
            Assert.AreEqual(sun.Scale.x, sun.Scale.z, Tol, "the sun core is a CUBE");
            Assert.AreEqual(RingRadius * s.SunApparentDiameter,
                            sun.Scale.x * ScarabWingDais.SunApparentFactor, Tol,
                            "the authored number is the size you SEE");
        }

        [Test]
        public void Generate_LaysExactlyTheAdvertisedPrismCount()
        {
            var s = ScarabWingDaisSettings.Default;
            var built = Build(s);
            Assert.AreEqual(s.PrismCount, built.Count,
                "PrismCount is what the budget and collider-cost notes are quoted from");
            Assert.AreEqual(s.PairCount * (2 * s.BladesPerWing + 1), built.Count);
            Assert.AreEqual(s.PairCount, built.FindAll(e => e.IsSunCore).Count, "one sun core per pair");
        }

        [Test]
        public void BladesGrowMonotonicallyAlongTheWing()
        {
            var s = ScarabWingDaisSettings.Default;
            var byBlade = new Dictionary<int, float>();
            foreach (var e in Build(s))
            {
                if (e.IsSunCore || byBlade.ContainsKey(e.Feather)) continue;
                float fit = e.Kind == PrismKind.Shielded ? ScarabWingDais.ShieldedFit : 1f;
                byBlade[e.Feather] = e.Scale.z / fit;    // LENGTH is the shared ramp; width is not
            }
            for (int b = 1; b < s.BladesPerWing; b++)
                Assert.Greater(byBlade[b], byBlade[b - 1], $"blade {b} must be longer than blade {b - 1}");
        }

        [Test]
        public void NothingSitsInsideTheSwitchesMouth()
        {
            var s = ScarabWingDaisSettings.Default;
            var (axis, u, v) = Frame();
            foreach (var e in Build(s))
                foreach (var q in Silhouette(e, u, v))
                    Assert.Greater(q.magnitude, RingRadius,
                        "the dais surrounds the switch; it never fills the mouth a ball threads");
            Assert.AreEqual(1f, Vector3.Dot(Vector3.Cross(u, v), axis), 1e-4f, "basis is right-handed");
        }

        [Test]
        public void OuterReachIsExactNotABound()
        {
            // The dish is keyed on it, so a loose bound would flatten the rosette rather than
            // merely over-reserving space.
            var s = ScarabWingDaisSettings.Default;
            var (_, u, v) = Frame();
            float reach = ScarabWingDais.OuterReach(s, RingRadius);
            float measured = 0f;
            foreach (var e in Build(s))
                foreach (var q in Silhouette(e, u, v))
                    measured = Mathf.Max(measured, q.magnitude);

            Assert.LessOrEqual(measured, reach + 0.5f, "OuterReach must bound the rosette");
            Assert.Greater(measured, reach * 0.9f, "…and must not overstate it by more than a hair");
            Assert.Greater(reach, RingRadius, "a dais that does not leave the ring is not a dais");
        }

        [Test]
        public void EverythingScalesWithTheRingRadius()
        {
            var s = ScarabWingDaisSettings.Default;
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
        public void BladesStateSizesThePrismPoolWouldOtherwiseClampAway()
        {
            // PrismScaleAnimator.SetTargetScale clamps into [minScale, maxScale] —
            // (0.5,0.5,0.5)..(40,10,10) on the interactive prism — silently. Without
            // AdmitTargetScale the dais is a field of stubs with no error anywhere.
            var s = ScarabWingDaisSettings.Default;
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
            // Every peer rebuilds the dais locally from a replicated input event; nothing about it
            // is networked, so identical inputs must give bit-identical output.
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
        public void Generate_OrdersOutwardSoTheWingsDrawThemselves()
        {
            var s = ScarabWingDaisSettings.Default;
            int lastBlade = -1;
            bool sunsStarted = false;
            foreach (var e in Build(s))
            {
                if (e.IsSunCore) { sunsStarted = true; continue; }
                Assert.IsFalse(sunsStarted, "the sun cores ignite LAST, after every blade");
                Assert.GreaterOrEqual(e.Feather, lastBlade, "blades are laid root-outward");
                lastBlade = e.Feather;
            }
            Assert.IsTrue(sunsStarted);
        }

        static ScarabWingDais.Element Find(List<ScarabWingDais.Element> built, int pair, int sign, int blade)
        {
            foreach (var e in built)
                if (e.Pair == pair && e.WingSign == sign && e.Feather == blade) return e;
            Assert.Fail($"no blade for pair {pair} wing {sign} index {blade}");
            return default;
        }
    }
}

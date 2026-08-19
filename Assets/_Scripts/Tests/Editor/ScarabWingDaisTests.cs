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
    /// against the real SHIELD silhouettes, not the boxes), that each pair is CONFINED to its own
    /// sector so overlap between pairs is impossible rather than merely absent, that the wings
    /// BEGIN at the switch ring and WRAP their sun, that the fan opens widest at the octahedral
    /// hinges, that a tier change is not a size change, that the sun core's authored size means
    /// what the tooltip says it means, and that the whole thing scales with the ring.
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
                // The stellation's convex hull IS the cube its spikes corner, so the outline is
                // the hull of those eight points under the sun's OWN rotation — which is no
                // longer axis-aligned. A hard-coded octagon would be measuring a shape the game
                // does not draw.
                var projected = new List<Vector2>(8);
                foreach (var tip in ScarabWingDais.SunSpikeTipsPerEdge)
                {
                    Vector3 w = e.Rotation * Vector3.Scale(tip, e.Scale);
                    projected.Add(p + new Vector2(Vector3.Dot(w, u), Vector3.Dot(w, v)));
                }
                poly.AddRange(ConvexHull(projected));
            }
            else
            {
                poly.Add(p + d * hl + n * hw); poly.Add(p + d * hl - n * hw);
                poly.Add(p - d * hl - n * hw); poly.Add(p - d * hl + n * hw);
            }
            return poly;
        }

        /// <summary>Monotone-chain hull, counter-clockwise. The SAT below wants a convex ring,
        /// and a projected cube's corners do not arrive in order.</summary>
        static List<Vector2> ConvexHull(List<Vector2> pts)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var hull = new List<Vector2>(pts.Count * 2);
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    var q = pass == 0 ? pts[i] : pts[pts.Count - 1 - i];
                    while (hull.Count - start >= 2)
                    {
                        Vector2 a = hull[hull.Count - 2], b = hull[hull.Count - 1];
                        if ((b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x) > 1e-9f) break;
                        hull.RemoveAt(hull.Count - 1);
                    }
                    hull.Add(q);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull;
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

        /// <summary>Angle of a point about the dais centre, in the (u,v) plane.</summary>
        static float PlanarAngle(Vector2 p) => Mathf.Atan2(p.y, p.x);

        static Vector2 Planar(Vector3 world, Vector3 u, Vector3 v) =>
            new(Vector3.Dot(world - Center, u), Vector3.Dot(world - Center, v));

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
        public void EveryPairStaysInsideItsOwnSectorOfTheDais()
        {
            // This is WHY the rosette cannot self-intersect between pairs, and it is a different
            // argument from the within-a-wing one: every blade is clipped to ScarabWingDais
            // .SectorLimit, so a pair physically cannot reach its neighbour. Overlap is then
            // impossible rather than merely absent from this parameter set.
            var s = ScarabWingDaisSettings.Default;
            var (_, u, v) = Frame();
            float half = Mathf.PI / s.PairCount;

            foreach (var e in Build(s))
            {
                if (e.IsSunCore) continue;
                float pairAngle = e.Pair * Mathf.PI * 2f / s.PairCount;
                foreach (var q in Silhouette(e, u, v))
                {
                    float d = Mathf.DeltaAngle(pairAngle * Mathf.Rad2Deg, PlanarAngle(q) * Mathf.Rad2Deg);
                    Assert.LessOrEqual(Mathf.Abs(d), half * Mathf.Rad2Deg + 0.05f,
                        $"pair {e.Pair} wing {e.WingSign} blade {e.Feather} reaches {d:F2}deg off its axis, " +
                        $"past its own {half * Mathf.Rad2Deg:F1}deg sector");
                }
            }
        }

        [Test]
        public void SectorConfinementHoldsEvenWhenTheBladeDialsAreAbsurd()
        {
            // The clip is load-bearing, not decorative: ask for blades four times longer than the
            // rosette's own radius — AND a length floor just as absurd, since the floor is applied
            // before the clip and a version that clamped up afterwards let a generous floor push
            // blades straight out of their own sector — and the pairs still cannot touch. A
            // construction that only behaves at its shipped numbers is a coincidence, not an
            // invariant.
            var s = ScarabWingDaisSettings.Default;
            s.BladeTipLength = 20f;
            s.BladeMinLength = 8f;
            s.BladeTaper = 0.2f;
            var (_, u, v) = Frame();
            var built = Build(s);
            var sil = new List<List<Vector2>>(built.Count);
            foreach (var e in built) sil.Add(Silhouette(e, u, v));

            for (int i = 0; i < sil.Count; i++)
            for (int j = i + 1; j < sil.Count; j++)
                if (built[i].Pair != built[j].Pair)
                    Assert.IsTrue(Separated(sil[i], sil[j]),
                        $"pair {built[i].Pair} reached into pair {built[j].Pair}");
        }

        [Test]
        public void EveryWingBeginsAtTheSwitchRing()
        {
            // The wing starts where the ball threaded the switch — the inboard spar's tip lands on
            // the ring — and that length is DERIVED from WingRootReach, never authored, so the two
            // cannot drift apart.
            var s = ScarabWingDaisSettings.Default;
            var (_, u, v) = Frame();
            float nearest = float.MaxValue;
            int nearestBlade = -1;
            foreach (var e in Build(s))
            {
                if (e.IsSunCore) continue;
                foreach (var q in Silhouette(e, u, v))
                    if (q.magnitude < nearest) { nearest = q.magnitude; nearestBlade = e.Feather; }
            }

            Assert.AreEqual(0, nearestBlade, "the blade nearest the switch must be the wing's FIRST");
            Assert.Greater(nearest, RingRadius, "and it must stop OUTSIDE the mouth a ball threads");
            // Stated against the rosette rather than against a fixed multiple of the ring, because
            // WingRootReach is a dial and WingHalfGapDeg swings the spar's tip off-axis on top of
            // it — the invariant is "the wings begin at the ring", not "at exactly 1.0x the ring".
            Assert.Less(nearest, ScarabWingDais.OuterReach(s, RingRadius) * 0.25f,
                $"the wings begin {nearest:F1} out from a ring of {RingRadius:F0} — that is a void, " +
                "not a rosette that starts at the switch");
            Assert.AreEqual(nearest, ScarabWingDais.InnerReach(s, RingRadius), 1e-2f,
                "InnerReach is what the Dais Lab reports; it must be the real thing");
        }

        [Test]
        public void WingRootReachMovesWhereTheWingsBegin()
        {
            var s = ScarabWingDaisSettings.Default;
            float near = ScarabWingDais.InnerReach(s, RingRadius);
            s.WingRootReach *= 3f;
            float far = ScarabWingDais.InnerReach(s, RingRadius);
            Assert.Greater(far, near + RingRadius, "the derived spar has to actually shorten");
        }

        [Test]
        public void EveryPairWrapsItsOwnSun()
        {
            // The sketch's C: the sun is not the root the wings sprout from and not a bead sitting
            // beside them — the pair closes around it. Measured as the angle its own blades span
            // SEEN FROM THE SUN, which is the only reading that cannot be satisfied by a wing
            // merely passing nearby.
            var s = ScarabWingDaisSettings.Default;
            var (_, u, v) = Frame();
            var built = Build(s);

            foreach (var sun in built.FindAll(e => e.IsSunCore))
            {
                Vector2 c = Planar(sun.Position, u, v);
                var angles = new List<float>();
                foreach (var b in built)
                    if (!b.IsSunCore && b.Pair == sun.Pair)
                        angles.Add(PlanarAngle(Planar(b.Position, u, v) - c));
                angles.Sort();

                float widestGap = angles[0] + Mathf.PI * 2f - angles[angles.Count - 1];
                for (int i = 1; i < angles.Count; i++)
                    widestGap = Mathf.Max(widestGap, angles[i] - angles[i - 1]);
                float wrap = (Mathf.PI * 2f - widestGap) * Mathf.Rad2Deg;

                Assert.Greater(wrap, 180f, $"pair {sun.Pair} only wraps {wrap:F0}deg of its sun — that is a fan beside it, not a cradle");
                Assert.Less(wrap, 360f, "the wings leave a mouth on the far side; a closed ring is a wreath, not a wing pair");
            }
            Assert.AreEqual(ScarabWingDais.WrapDegrees(s, RingRadius),
                            2f * ScarabWingDais.BuildWing(s, RingRadius)[s.BladesPerWing - 1].Theta * Mathf.Rad2Deg,
                            1e-3f);
        }

        [Test]
        public void EverySunAimsASpikeAtTheSwitch()
        {
            // A sun core is a stella octangula, so its eight spikes point at the CUBE CORNERS.
            // Aiming its (1,1,1) body diagonal inboard puts one of those spikes on the line from
            // the sun to the spent switch: every sun points AT the thing it rings, which is the
            // one direction in the rosette that means anything.
            Assert.AreEqual(0f,
                (ScarabWingDais.SunCornerAim * Vector3.one.normalized - Vector3.forward).magnitude,
                1e-5f, "SunCornerAim must take the unit body diagonal onto local +z");

            var s = ScarabWingDaisSettings.Default;
            var (axis, _, _) = Frame();
            float edge = ScarabWingDais.SunEdge(s, RingRadius);
            float spikeReach = edge * ScarabWingDais.SunInPlaneReach;

            foreach (var sun in Build(s).FindAll(e => e.IsSunCore))
            {
                Vector3 inward = -Vector3.ProjectOnPlane(sun.Position - Center, axis).normalized;
                Vector3 spike = sun.Rotation * (Vector3.one.normalized * spikeReach);
                Assert.Less(Vector3.Angle(spike, inward), 0.05f,
                    $"sun {sun.Pair}'s (1,1,1) spike is {Vector3.Angle(spike, inward):F2}deg off the switch");
                // …and it is IN the dais plane, which is why the aim costs clearance (below).
                Assert.AreEqual(0f, Vector3.Dot(spike.normalized, axis), 1e-4f);
            }
        }

        [Test]
        public void TheSunSitsClearInsideTheHoleItsWingsWrap()
        {
            // A stella's IN-PLANE reach is the one of its three sizes that decides whether the
            // wings grow through their own sun — and aiming a spike at the switch (above) is what
            // makes it the FULL circumradius rather than the axis-aligned pose's
            // CIRCUMSCRIBING_SCALE·√2/2. A rotation that nobody costed is a collision.
            Assert.AreEqual(ScarabWingDais.SunApparentFactor * 0.5f,
                            ScarabWingDais.SunInPlaneReach, 1e-5f,
                            "an aimed sun reaches its full circumradius in the dais plane");

            var s = ScarabWingDaisSettings.Default;
            Assert.Greater(ScarabWingDais.SunClearance(s, RingRadius), 0f,
                "the sun's spikes are inside the ring of blade roots that wraps it");

            var (_, u, v) = Frame();
            var built = Build(s);
            float hole = RingRadius * s.WingHoleRadius;
            foreach (var sun in built.FindAll(e => e.IsSunCore))
            {
                Vector2 c = Planar(sun.Position, u, v);
                foreach (var b in built)
                {
                    if (b.IsSunCore || b.Pair != sun.Pair) continue;
                    foreach (var q in Silhouette(b, u, v))
                        Assert.GreaterOrEqual((q - c).magnitude, hole - 1e-2f,
                            $"blade {b.Feather} intrudes into the hole its own sun occupies");
                }
            }
        }

        [Test]
        public void TheFanOpensWidestAtTheOctahedralHinges()
        {
            // A plain blade is a rectangle: its root CORNER is the widest thing about it, so it
            // stands its neighbour off by atan(halfWidth/hole) — a couple of degrees, and no more
            // however long it is. An octahedron presents a root POINT with faces sloping at
            // atan(w/L) from its axis, so a neighbour flush against one of those faces has to
            // stand off by that whole angle. That is why the wing's curve is PLACED by the tier
            // pattern rather than tuned, and why the shape the eye reads and the pattern it reads
            // are one statement.
            //
            // The exception is honest and worth naming: on the INBOARD SPAR the octahedron is a
            // needle (atan(2.5/95) is under two degrees), so blade 0's cap does not open anything
            // — it is an accent marking where the wing begins. The mechanic belongs to the wrap,
            // where the blades are short, so that is where it is asserted.
            var s = ScarabWingDaisSettings.Default;
            var wing = ScarabWingDais.BuildWing(s, RingRadius);
            int checkedHinges = 0;

            float firstRatio = 0f, lastRatio = 0f;
            for (int h = s.HingeEvery; h < wing.Count - 1; h += s.HingeEvery)
            {
                if (wing[h].Kind != PrismKind.Shielded) continue;
                float hingeJunction = wing[h].Theta - wing[h - 1].Theta;
                float plainJunction = wing[h + 2].Theta - wing[h + 1].Theta;
                Assert.Greater(hingeJunction, plainJunction,
                    $"the fan opens {hingeJunction * Mathf.Rad2Deg:F2}deg at hinge {h} but " +
                    $"{plainJunction * Mathf.Rad2Deg:F2}deg between the plain blades beside it — " +
                    "the octahedra are decoration, not joints");
                lastRatio = hingeJunction / plainJunction;
                if (checkedHinges == 0) firstRatio = lastRatio;
                checkedHinges++;
            }
            Assert.Greater(checkedHinges, 1, "a wing with one joint is a kink, not a curve");

            // The mechanic STRENGTHENS as the wing wraps, because a hinge's stand-off is
            // atan(w/L) and the blades are shortening. A wing whose last joint opened no wider
            // than its first would mean the widths had stopped tracking the lengths.
            Assert.Greater(lastRatio, firstRatio * 1.2f,
                $"the hinges open {firstRatio:F2}x the plain step at the root and {lastRatio:F2}x " +
                "at the tip — the joints should bite harder as the blades shorten");

            for (int i = 1; i < wing.Count; i++)
                Assert.Greater(wing[i].Theta, wing[i - 1].Theta, "the fan only ever opens");
        }

        [Test]
        public void HingeWidthScaleOpensTheFanWithoutTouchingThePlainBlades()
        {
            // The hinge carries its own width so the joint can be fattened — a stronger turn —
            // without thickening every feather in the wing.
            var s = ScarabWingDaisSettings.Default;
            var narrow = ScarabWingDais.BuildWing(s, RingRadius);
            s.HingeWidthScale *= 2f;
            var wide = ScarabWingDais.BuildWing(s, RingRadius);

            for (int i = 0; i < narrow.Count; i++)
            {
                if (narrow[i].Kind == PrismKind.Shielded)
                    Assert.AreEqual(narrow[i].Width * 2f, wide[i].Width, 1e-3f, "a hinge follows its own scale");
                else
                    Assert.AreEqual(narrow[i].Width, wide[i].Width, 1e-4f, "and no plain blade moves with it");
            }
            Assert.Greater(ScarabWingDais.WrapDegrees(s, RingRadius),
                           ScarabWingDais.WrapDegrees(ScarabWingDaisSettings.Default, RingRadius),
                           "a fatter hinge opens the fan further around the sun");
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
        public void TheInboardSparIsTheLongestBladeInTheWing()
        {
            // The wing's silhouette is a cardioid in the fan angle: longest where it reaches back
            // at the switch, easing as it closes around the sun. If some mid-wing blade were the
            // longest, the wing would read as a spearhead pointing sideways.
            var s = ScarabWingDaisSettings.Default;
            var wing = ScarabWingDais.BuildWing(s, RingRadius);
            for (int i = 1; i < wing.Count; i++)
                Assert.Less(wing[i].Length, wing[0].Length,
                    $"blade {i} out-reaches the spar that is supposed to touch the ring");
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
            bool overCeiling = false;
            foreach (var e in Build(s))
                if (!e.IsSunCore) overCeiling |= e.Scale.z > 10f;
            Assert.IsTrue(overCeiling, "the long spars must exceed the pool's default axis ceiling");

            // The FLOOR is the other end of the same clamp and it is one dial away: a plate at the
            // fleet-standard thickness, once fitted by ShieldedFit, lands under 0.5. The shipped
            // dais happens to sit above it, which is exactly why this is asserted against a
            // variant rather than assumed to be unreachable.
            s.BladeThickness = 0.05f;
            bool underFloor = false;
            foreach (var e in Build(s))
                if (!e.IsSunCore)
                    underFloor |= Mathf.Min(e.Scale.x, Mathf.Min(e.Scale.y, e.Scale.z)) < 0.5f;
            Assert.IsTrue(underFloor, "a thin-plate dais must go under the pool's floor");
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
    }
}

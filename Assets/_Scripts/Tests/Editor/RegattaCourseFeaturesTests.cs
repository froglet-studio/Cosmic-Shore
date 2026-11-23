#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Regatta course features (<see cref="RegattaCourseFeatures"/>: rail sections, neutral colour, gaps, detours,
    /// alternate rings, line crystals) keep the arena's contract. An EMPTY feature set lays the course exactly as it was;
    /// a detour swings the braid off the line but every lane still threads every ring at the lane offset; a gap splits a
    /// lane into open runs and lays nothing inside it; an alternate ring stands in the course ring's plane clear of its
    /// mouth; line crystals sit on the spine, evenly, never more than the cap.
    ///
    /// <para>No Unity is needed to run these: Tools/Build/regatta_course_harness/features.sh compiles this file against
    /// the harness's UnityEngine and NUnit stubs and runs every [Test] (the cloud sessions have no editor).</para>
    /// </summary>
    public class RegattaCourseFeaturesTests
    {
        const float PrismHalfWidth = 3f;

        static List<RaceGate> Shipped(int intensity) => RegattaCourse.BuildGates(RegattaCourse.DefaultSeed, intensity);

        static RegattaCourseFeatures Busy()
        {
            var f = new RegattaCourseFeatures();
            f.Sections.Add(new RegattaRailSection(-1, 0.3f, 0.7f, neutral: true));
            f.Sections.Add(new RegattaRailSection(2, 0.4f, 0.6f, RegattaRailKind.Gap));
            f.Sections.Add(new RegattaRailSection(5, 0.2f, 0.8f, RegattaRailKind.Plain, detour: 120f, detourBearing: 30f));
            f.Sections.Add(new RegattaRailSection(6, 0.1f, 0.5f, RegattaRailKind.Shielded));
            f.AlternateGates.Add(new RegattaAlternateGate(3, 200f, 0f, 0.8f));
            f.AlternateGates.Add(new RegattaAlternateGate(6, 180f, 90f));
            f.Crystals = new RegattaLineCrystals { PerLap = 3, Phase = 0.1f };
            return f;
        }

        [Test]
        public void EmptyFeatures_LayTheCourseAsItWas()
        {
            for (int i = 1; i <= 4; i++)
            {
                var gates = Shipped(i);
                var before = RegattaCourse.BuildRails(gates, RegattaCourse.DefaultRails);
                var after = RegattaCourse.BuildRails(gates, RegattaCourse.DefaultRails, RegattaCourseFeatures.None);
                Assert.AreEqual(before.LanePositions.Count, after.LanePositions.Count, $"I{i}: lane count");
                for (int lane = 0; lane < before.LanePositions.Count; lane++)
                {
                    var a = before.LanePositions[lane];
                    var b = after.LanePositions[lane];
                    Assert.AreEqual(a.Length, b.Length, $"I{i} lane {lane}: prism count");
                    for (int k = 0; k < a.Length; k++)
                        Assert.IsTrue(a[k].x == b[k].x && a[k].y == b[k].y && a[k].z == b[k].z,
                            $"I{i} lane {lane} prism {k}: moved by an empty feature set");

                    var plan = RegattaRailPlan.ForLane(after.LaneLegParams[lane], gates.Count, RegattaCourseFeatures.None);
                    Assert.AreEqual(1, plan.Runs.Count, $"I{i} lane {lane}: one run");
                    Assert.IsTrue(plan.Runs[0].Closed, $"I{i} lane {lane}: the run is the closed loop it always was");
                    Assert.AreEqual(a.Length, plan.Runs[0].Count, $"I{i} lane {lane}: every prism laid");
                    for (int k = 0; k < a.Length; k++)
                    {
                        Assert.AreEqual(PrismKind.SuperShielded, plan.Kinds[k], $"I{i} lane {lane} prism {k}: kind");
                        Assert.IsFalse(plan.Neutral[k], $"I{i} lane {lane} prism {k}: colour");
                    }
                }
                var none = RegattaCourseFeatures.None;
                Assert.IsTrue(none.IsEmpty, "None is empty");
                for (int g = 0; g < gates.Count; g++)
                    Assert.IsFalse(none.TryAlternate(gates, g, out _), $"I{i} gate {g}: no alternate");
                Assert.AreEqual(0, none.LineCrystalPoints(after).Count, $"I{i}: no line crystals");
            }
            Assert.IsTrue(RegattaCourseFeatures.NeutralMiddle(0f).IsEmpty, "a zero neutral share is no section");
        }

        [Test]
        public void LegParams_RunOnceRoundTheLap_InOrder()
        {
            for (int i = 1; i <= 4; i++)
            {
                var gates = Shipped(i);
                var rails = RegattaCourse.BuildRails(gates, RegattaCourse.DefaultRails, RegattaCourseFeatures.None);
                foreach (var p in rails.LaneLegParams)
                {
                    Assert.Less(p[0], 0.05f, $"I{i}: a lane starts at gate 0");
                    for (int k = 1; k < p.Length; k++)
                        Assert.Greater(p[k], p[k - 1], $"I{i}: leg params rise along the lane (prism {k})");
                    Assert.Less(p[p.Length - 1], gates.Count, $"I{i}: and stay inside one lap");
                    Assert.Greater(p[p.Length - 1], gates.Count - 0.05f, $"I{i}: and reach its end");
                }
            }
        }

        [Test]
        public void Detour_SwingsTheBraidOffTheLine_ButEveryLaneStillThreadsEveryRing()
        {
            var f = new RegattaCourseFeatures();
            f.Sections.Add(new RegattaRailSection(-1, 0.15f, 0.85f, detour: 90f, detourBearing: 45f));
            for (int i = 1; i <= 4; i++)
            {
                var gates = Shipped(i);
                var rs = RegattaCourse.DefaultRails;
                var plain = RegattaCourse.BuildRails(gates, rs);
                var bent = RegattaCourse.BuildRails(gates, rs, f);
                for (int lane = 0; lane < bent.LanePositions.Count; lane++)
                {
                    Assert.Greater(bent.LaneLengths[lane], plain.LaneLengths[lane], $"I{i} lane {lane}: the detour is longer");
                    AssertThreadsEveryRing(gates, bent.LanePositions[lane], rs.LaneOffset, rs.PrismSpacing, $"I{i} lane {lane}");
                }
                float spread = Spread(bent.LaneLengths);
                Assert.Less(spread, 0.05f, $"I{i}: the three detoured lanes stay within 5% of one length");
                // Mid-leg the braid stands about the detour off its old place.
                float far = 0f;
                var p = bent.LanePositions[0];
                var q = plain.LanePositions[0];
                for (int k = 0; k < Mathf.Min(p.Length, q.Length); k += 4) far = Mathf.Max(far, Nearest(q, p[k]));
                Assert.Greater(far, 60f, $"I{i}: a 90 u detour moved the lane (furthest {far:F0} u from the old lane)");
            }
        }

        [Test]
        public void Gap_SplitsTheLane_LaysNothingInside_AndTheRestStillThreadsTheRings()
        {
            var f = new RegattaCourseFeatures();
            f.Sections.Add(new RegattaRailSection(1, 0.35f, 0.65f, RegattaRailKind.Gap));
            f.Sections.Add(new RegattaRailSection(4, 0.35f, 0.65f, RegattaRailKind.Gap));
            for (int i = 1; i <= 4; i++)
            {
                var gates = Shipped(i);
                var rails = RegattaCourse.BuildRails(gates, RegattaCourse.DefaultRails, f);
                for (int lane = 0; lane < rails.LanePositions.Count; lane++)
                {
                    var plan = RegattaRailPlan.ForLane(rails.LaneLegParams[lane], gates.Count, f);
                    Assert.AreEqual(2, plan.Runs.Count, $"I{i} lane {lane}: two gaps cut the loop into two runs");
                    foreach (var run in plan.Runs) Assert.IsFalse(run.Closed, $"I{i} lane {lane}: a run with a gap is open");
                    int gapped = 0;
                    var p = rails.LaneLegParams[lane];
                    for (int k = 0; k < p.Length; k++)
                    {
                        int leg = (int)p[k];
                        float share = p[k] - leg;
                        bool inside = (leg == 1 || leg == 4) && share >= 0.35f && share < 0.65f;
                        Assert.AreEqual(inside, plan.Gap[k], $"I{i} lane {lane} prism {k}: gap membership");
                        if (inside) gapped++;
                    }
                    Assert.AreEqual(p.Length - gapped, plan.LaidCount, $"I{i} lane {lane}: every prism outside the gaps is laid once");
                    Assert.Greater(gapped, 2 * RegattaCourseFeatures.MinGapPrisms, $"I{i} lane {lane}: the gaps are real");
                    // The rings sit at leg ends, so the laid rail still threads every one of them.
                    var laid = new List<Vector3>();
                    foreach (var run in plan.Runs)
                        for (int q = 0; q < run.Count; q++) laid.Add(rails.LanePositions[lane][(run.Start + q) % p.Length]);
                    AssertThreadsEveryRing(gates, laid.ToArray(), RegattaCourse.DefaultRails.LaneOffset,
                                           RegattaCourse.DefaultRails.PrismSpacing, $"I{i} lane {lane} (gapped)");
                }
            }
        }

        [Test]
        public void ShortGap_IsLaidAsRail()
        {
            var gates = Shipped(1);
            var rails = RegattaCourse.BuildRails(gates, RegattaCourse.DefaultRails, null);
            float legArcShare = 4f / rails.SpineLength * gates.Count;   // about half a prism of one leg
            var f = new RegattaCourseFeatures();
            f.Sections.Add(new RegattaRailSection(2, 0.5f, 0.5f + legArcShare, RegattaRailKind.Gap));
            var plan = RegattaRailPlan.ForLane(rails.LaneLegParams[0], gates.Count, f);
            Assert.AreEqual(1, plan.Runs.Count, "a hole shorter than MinGapPrisms does not split the lane");
            Assert.IsTrue(plan.Runs[0].Closed);
            Assert.AreEqual(rails.LanePositions[0].Length, plan.LaidCount);
        }

        [Test]
        public void Sections_SetKindAndColour_OnlyWhereTheyCover_LaterWins()
        {
            var gates = Shipped(2);
            var f = Busy();
            var rails = RegattaCourse.BuildRails(gates, RegattaCourse.DefaultRails, f);
            var p = rails.LaneLegParams[1];
            var plan = RegattaRailPlan.ForLane(p, gates.Count, f);
            int neutral = 0, plainCount = 0, shielded = 0;
            for (int k = 0; k < p.Length; k++)
            {
                int leg = (int)p[k];
                float s = p[k] - leg;
                bool inNeutral = s >= 0.3f && s < 0.7f;
                bool inPlain = leg == 5 && s >= 0.2f && s < 0.8f;
                bool inShield = leg == 6 && s >= 0.1f && s < 0.5f;
                bool inGap = leg == 2 && s >= 0.4f && s < 0.6f;
                // Later entries win: the plain detour and the shielded stretch replace the neutral one where they overlap.
                bool expectNeutral = inNeutral && !inPlain && !inShield && !inGap;
                if (!inGap) Assert.AreEqual(expectNeutral, plan.Neutral[k], $"prism {k} (leg {leg} share {s:F2}): colour");
                var expectKind = inPlain ? PrismKind.Plain : inShield ? PrismKind.Shielded : PrismKind.SuperShielded;
                if (!inGap) Assert.AreEqual(expectKind, plan.Kinds[k], $"prism {k} (leg {leg} share {s:F2}): kind");
                if (plan.Neutral[k]) neutral++;
                if (plan.Kinds[k] == PrismKind.Plain) plainCount++;
                if (plan.Kinds[k] == PrismKind.Shielded) shielded++;
            }
            Assert.Greater(neutral, 0);
            Assert.Greater(plainCount, 0);
            Assert.Greater(shielded, 0);
            Assert.IsFalse(plan.Neutral[0], "a lane still starts in its team's colour at gate 0");
            Assert.AreNotEqual(RegattaCourseFeatures.NeutralDomain, RegattaCourse.LaneDomains[0]);
            foreach (var d in RegattaCourse.LaneDomains)
                Assert.AreNotEqual(RegattaCourseFeatures.NeutralDomain, d, "the neutral colour is no team's lane");
        }

        [Test]
        public void AlternateRing_StandsInTheRingsPlane_ClearOfItsMouth()
        {
            var f = Busy();
            for (int i = 1; i <= 4; i++)
            {
                var gates = Shipped(i);
                int found = 0;
                for (int g = 0; g < gates.Count; g++)
                {
                    if (!f.TryAlternate(gates, g, out var alt)) continue;
                    found++;
                    var ring = gates[g];
                    Vector3 d = alt.Position - ring.Position;
                    Assert.Less(Mathf.Abs(Vector3.Dot(d, ring.Axis.normalized)), 0.01f, $"I{i} gate {g}: in the ring's plane");
                    Assert.Less(Vector3.Angle(alt.Axis, ring.Axis), 0.01f, $"I{i} gate {g}: facing the same way");
                    Assert.Greater(d.magnitude, ring.Radius + alt.Radius, $"I{i} gate {g}: the two mouths do not overlap");
                    // No lane threads the alternate: the rails stay the course ring's.
                    var rails = RegattaCourse.BuildRails(gates, RegattaCourse.DefaultRails, f);
                    foreach (var lane in rails.LanePositions)
                        foreach (var q in lane)
                        {
                            Vector3 r = q - alt.Position;
                            float along = Vector3.Dot(r, alt.Axis.normalized);
                            float lat = (r - alt.Axis.normalized * along).magnitude;
                            Assert.IsFalse(Mathf.Abs(along) < 4f && lat < alt.Radius - PrismHalfWidth,
                                $"I{i} gate {g}: a lane passes inside the alternate's mouth");
                        }
                }
                Assert.AreEqual(2, found, $"I{i}: both alternates stand");
            }
        }

        [Test]
        public void AlternateChoice_TakesTheShorterWay_OnlyByTheMargin()
        {
            var self = new Vector3(0f, 0f, -500f);
            var ring = new Vector3(0f, 0f, 0f);
            var next = new Vector3(400f, 0f, 400f);
            var nearer = new Vector3(150f, 0f, 0f);
            var further = new Vector3(-150f, 0f, 0f);
            Assert.IsTrue(RegattaAlternateChoice.TakesAlternate(self, ring, nearer, next, 0.05f));
            Assert.IsFalse(RegattaAlternateChoice.TakesAlternate(self, ring, further, next, 0.05f));
            Assert.IsFalse(RegattaAlternateChoice.TakesAlternate(self, ring, ring, next, 0f), "a tie keeps the course ring");
        }

        [Test]
        public void LineCrystals_SitOnTheSpine_Evenly_Capped()
        {
            for (int i = 1; i <= 4; i++)
            {
                var gates = Shipped(i);
                var f = new RegattaCourseFeatures { Crystals = new RegattaLineCrystals { PerLap = 3, Phase = 0.1f } };
                var rails = RegattaCourse.BuildRails(gates, RegattaCourse.DefaultRails, f);
                var points = f.LineCrystalPoints(rails);
                Assert.AreEqual(3, points.Count, $"I{i}: three crystals");
                foreach (var c in points)
                    Assert.Less(Nearest(rails.SpinePositions, c), 12f, $"I{i}: a crystal sits on the spine");

                f.Crystals.PerLap = 40;
                Assert.AreEqual(RegattaCourseFeatures.MaxLineCrystalsPerLap, f.LineCrystalPoints(rails).Count,
                                $"I{i}: the count is capped (use sparingly)");
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        static void AssertThreadsEveryRing(List<RaceGate> gates, Vector3[] lane, float offset, float spacing, string what)
        {
            for (int g = 0; g < gates.Count; g++)
            {
                var gate = gates[g];
                float bestDist = float.MaxValue, plane = 0f, lat = 0f;
                foreach (var p in lane)
                {
                    var d = p - gate.Position;
                    float dist = d.magnitude;
                    if (dist >= bestDist) continue;
                    bestDist = dist;
                    float along = Vector3.Dot(d, gate.Axis);
                    plane = Mathf.Abs(along);
                    lat = (d - gate.Axis * along).magnitude;
                }
                Assert.LessOrEqual(plane, spacing, $"{what} gate {g}: the rail crosses the ring plane");
                Assert.LessOrEqual(Mathf.Abs(lat - offset), spacing, $"{what} gate {g}: at the lane offset ({lat:F1} u)");
                Assert.Less(lat + PrismHalfWidth, gate.Radius, $"{what} gate {g}: inside the mouth");
            }
        }

        static float Nearest(Vector3[] points, Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var q in points) best = Mathf.Min(best, (q - p).magnitude);
            return best;
        }

        static float Spread(List<float> lengths)
        {
            float mn = float.MaxValue, mx = 0f;
            foreach (var l in lengths) { mn = Mathf.Min(mn, l); mx = Mathf.Max(mx, l); }
            return (mx - mn) / mn;
        }
    }
}
#endif

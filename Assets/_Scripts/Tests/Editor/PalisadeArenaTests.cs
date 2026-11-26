using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Palisade's arena, one test per placement rule (PALISADE.md section 9). Pure, so the same file runs in the editor
    /// and headless in <c>Tools/Build/palisade_harness/run.sh</c>, whose negative controls break each rule in turn.
    /// </summary>
    [TestFixture]
    public class PalisadeArenaTests
    {
        static void Lay(int intensity, out List<PalisadeArenaPart> parts, out List<PalisadeLane> lanes)
        {
            parts = new List<PalisadeArenaPart>();
            lanes = new List<PalisadeLane>();
            PalisadeArena.Lay(PalisadeArena.ForIntensity(intensity), parts, lanes);
        }

        static int Count(List<PalisadeArenaPart> parts, int piece, PrismKind kind)
        {
            int n = 0;
            for (int i = 0; i < parts.Count; i++) if (parts[i].Piece == piece && parts[i].Kind == kind) n++;
            return n;
        }

        static int CountPiece(List<PalisadeArenaPart> parts, int piece)
        {
            int n = 0;
            for (int i = 0; i < parts.Count; i++) if (parts[i].Piece == piece) n++;
            return n;
        }

        /// <summary>What costs the Swordfish on a rung: every prism that takes two strokes or debuffs, outside the
        /// frames (a frame's shielded stones are cover, which helps).</summary>
        static int Cost(List<PalisadeArenaPart> parts)
        {
            int n = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p.Kind == PrismKind.Danger) n++;
                else if (p.Kind == PrismKind.Shielded && p.Piece != PalisadeArena.Frame) n++;
            }
            return n;
        }

        [Test]
        public void EveryPeerLaysTheSameArena()
        {
            for (int rung = 1; rung <= 4; rung++)
            {
                Lay(rung, out var a, out _);
                Lay(rung, out var b, out _);
                Assert.AreEqual(a.Count, b.Count, $"I{rung}");
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.IsTrue(a[i].Position == b[i].Position && a[i].Scale == b[i].Scale, $"I{rung} part {i} moved between two lays");
                    Assert.AreEqual(a[i].Kind, b[i].Kind, $"I{rung} part {i}");
                }
            }
        }

        [Test]
        public void EveryPartStandsOutsideTheSpawnRing_AndInsideTheMembrane()
        {
            for (int rung = 1; rung <= 4; rung++)
            {
                Lay(rung, out var parts, out _);
                Assert.Greater(parts.Count, 0, $"I{rung} has no arena");
                for (int i = 0; i < parts.Count; i++)
                {
                    float r = parts[i].Position.magnitude;
                    Assert.GreaterOrEqual(r, PalisadeArena.InnerRadius, $"I{rung} part {i} stands in the spawn ring");
                    Assert.LessOrEqual(r, PalisadeArena.OuterRadius, $"I{rung} part {i} stands at the membrane");
                }
            }
        }

        [Test]
        public void ALanesPostsAreAStraightEvenRow_InBladeReachOfTheRun_AndClearOfTheHull()
        {
            for (int rung = 1; rung <= 4; rung++)
            {
                Lay(rung, out var parts, out var lanes);
                for (int k = 0; k < lanes.Count; k++)
                {
                    var lane = lanes[k];
                    int seen = 0;
                    for (int i = 0; i < parts.Count; i++)
                    {
                        var p = parts[i];
                        if (p.Piece != PalisadeArena.Post || p.Lane != k) continue;
                        Vector3 along = lane.FirstPost + lane.Axis * (seen * PalisadeArena.PostSpacing);
                        Assert.IsTrue(p.Position == along, $"I{rung} lane {k} post {seen} is off the row");

                        // The run passes each post at RunOffset to its side: inside the blade, outside the hull.
                        Vector3 beside = p.Position + lane.Side * PalisadeArena.RunOffset;
                        float reach = Vector3.Distance(beside, p.Position);
                        Assert.Less(reach, PalisadeArena.BladeReach, $"I{rung} lane {k}: the blade does not reach the row");
                        float thinTowardRun = Mathf.Abs(Vector3.Dot(p.Forward, lane.Side)) * p.Scale.z * 0.5f
                                              + Mathf.Abs(Vector3.Dot(p.Up, lane.Side)) * p.Scale.y * 0.5f
                                              + p.Scale.x * 0.5f;
                        Assert.Less(thinTowardRun, PalisadeArena.RunOffset - 4f, $"I{rung} lane {k}: a post reaches into the run");
                        seen++;
                    }
                    Assert.AreEqual(PalisadeArena.ForIntensity(rung).PostsPerLane, seen, $"I{rung} lane {k}");
                    Assert.AreEqual(1f, lane.Axis.magnitude, 1e-3f);
                    Assert.AreEqual(0f, Vector3.Dot(lane.Axis, lane.Side), 1e-3f, "the run's side is square to the lane");
                }
            }
        }

        [Test]
        public void EveryLaneEndsInAFrameOnItsAxis_WideEnoughForAWall()
        {
            for (int rung = 1; rung <= 4; rung++)
            {
                Lay(rung, out var parts, out var lanes);
                for (int k = 0; k < lanes.Count; k++)
                {
                    var lane = lanes[k];
                    Vector3 toFrame = lane.FrameCentre - lane.LastPost;
                    Assert.AreEqual(PalisadeArena.FrameGap, Vector3.Dot(toFrame, lane.Axis), 1e-2f, $"I{rung} lane {k}: the frame is not past the row's end");
                    Assert.AreEqual(0f, Vector3.Cross(toFrame, lane.Axis).magnitude, 1e-2f, $"I{rung} lane {k}: the frame is off the lane's axis");

                    int stones = 0;
                    for (int i = 0; i < parts.Count; i++)
                    {
                        var p = parts[i];
                        if (p.Piece != PalisadeArena.Frame || p.Lane != k) continue;
                        stones++;
                        Vector3 off = p.Position - lane.FrameCentre;
                        Assert.AreEqual(0f, Vector3.Dot(off, lane.Axis), 1e-2f, $"I{rung} lane {k}: a stone is out of the hoop's plane");
                        // The opening: the stone's inner face is no nearer the centre than the frame's radius.
                        Assert.GreaterOrEqual(off.magnitude - p.Scale.x * 0.5f, PalisadeArena.FrameRadius - 1.6f, $"I{rung} lane {k}: a stone stands in the mouth");
                    }
                    Assert.AreEqual(PalisadeArena.StonesStanding(PalisadeArena.ForIntensity(rung).FrameClosure), stones, $"I{rung} lane {k}");
                }
                Assert.GreaterOrEqual(PalisadeArena.FrameRadius * 2f, PalisadeArena.BladeReach * 2f, "the mouth is narrower than two blades: no wall fits");
            }
        }

        [Test]
        public void HelpFalls_AndCostRises_OnEveryRung()
        {
            int lastPlain = int.MaxValue, lastCost = -1;
            float lastStraight = float.MaxValue;
            for (int rung = 1; rung <= 4; rung++)
            {
                Lay(rung, out var parts, out var lanes);
                int plain = Count(parts, PalisadeArena.Post, PrismKind.Plain);
                int cost = Cost(parts);
                float straight = 0f;
                for (int k = 0; k < lanes.Count; k++) straight += lanes[k].StraightLength;

                Assert.Less(plain, lastPlain, $"I{rung} has as much plain fuel as I{rung - 1}");
                Assert.Greater(cost, lastCost, $"I{rung} costs no more than I{rung - 1}");
                Assert.Less(straight, lastStraight, $"I{rung} has as much straight to wind the ramp as I{rung - 1}");
                lastPlain = plain;
                lastCost = cost;
                lastStraight = straight;
            }
        }

        [Test]
        public void I1OnlyHelps()
        {
            Lay(1, out var parts, out _);
            Assert.AreEqual(0, Cost(parts), "I1 has a prism that costs the Swordfish");
            Assert.AreEqual(0, CountPiece(parts, PalisadeArena.Rubble));
            Assert.AreEqual(PalisadeArena.FrameStones * PalisadeArena.ForIntensity(1).Lanes,
                Count(parts, PalisadeArena.Frame, PrismKind.Shielded), "every I1 frame is a closed shielded hoop");
        }

        [Test]
        public void AFrameClosesLessOnEveryRung_AndWhatIsMissingFacesTheCentre()
        {
            int last = int.MaxValue;
            for (int rung = 1; rung <= 4; rung++)
            {
                int standing = PalisadeArena.StonesStanding(PalisadeArena.ForIntensity(rung).FrameClosure);
                Assert.Less(standing, last, $"I{rung}'s frame is as closed as I{rung - 1}'s");
                last = standing;
                if (standing == PalisadeArena.FrameStones) continue;

                Lay(rung, out var parts, out var lanes);
                for (int k = 0; k < lanes.Count; k++)
                {
                    Vector3 sum = Vector3.zero;
                    for (int i = 0; i < parts.Count; i++)
                        if (parts[i].Piece == PalisadeArena.Frame && parts[i].Lane == k) sum += parts[i].Position - lanes[k].FrameCentre;
                    Assert.Greater(Vector3.Dot(sum, lanes[k].Side), 0f, $"I{rung} lane {k}: the standing arc is on the centre's side");
                }
            }
        }

        [Test]
        public void DangerStandsOnlyFromI3_OnlyAtAFramesTwoEndsOrInRubble()
        {
            for (int rung = 1; rung <= 4; rung++)
            {
                Lay(rung, out var parts, out var lanes);
                int danger = 0;
                for (int i = 0; i < parts.Count; i++) if (parts[i].Kind == PrismKind.Danger) danger++;
                if (rung < 3) { Assert.AreEqual(0, danger, $"I{rung} has danger prisms"); continue; }

                Assert.AreEqual(0, Count(parts, PalisadeArena.Post, PrismKind.Danger), "a danger post would punish the lane's own lesson");
                Assert.AreEqual(2 * lanes.Count, Count(parts, PalisadeArena.Frame, PrismKind.Danger), $"I{rung}: two tips a frame");
                for (int i = 0; i < parts.Count; i++)
                {
                    if (parts[i].Piece != PalisadeArena.Frame) continue;
                    bool first = i == 0 || parts[i - 1].Piece != PalisadeArena.Frame || parts[i - 1].Lane != parts[i].Lane;
                    bool lastStone = i == parts.Count - 1 || parts[i + 1].Piece != PalisadeArena.Frame || parts[i + 1].Lane != parts[i].Lane;
                    Assert.AreEqual(first || lastStone, parts[i].Kind == PrismKind.Danger, $"I{rung} frame stone {i}");
                }
            }
            Lay(3, out var three, out _);
            Assert.AreEqual(0, Count(three, PalisadeArena.Rubble, PrismKind.Danger), "I3's rubble is shielded only");
            Lay(4, out var four, out _);
            Assert.Greater(Count(four, PalisadeArena.Rubble, PrismKind.Danger), 0, "I4's rubble carries danger");
        }

        [Test]
        public void RubbleIsAllLaid_AndKeepsOffTheLanesAndEachOther()
        {
            for (int rung = 1; rung <= 4; rung++)
            {
                Lay(rung, out var parts, out var lanes);
                Assert.AreEqual(PalisadeArena.ForIntensity(rung).Rubble, CountPiece(parts, PalisadeArena.Rubble), $"I{rung}: a chunk found no room");
                for (int i = 0; i < parts.Count; i++)
                {
                    if (parts[i].Piece != PalisadeArena.Rubble) continue;
                    for (int k = 0; k < lanes.Count; k++)
                        Assert.GreaterOrEqual(PalisadeArena.DistanceToRun(parts[i].Position, lanes[k]), PalisadeArena.RubbleLaneClearance,
                            $"I{rung}: rubble {i} stands in lane {k}");
                    for (int j = i + 1; j < parts.Count; j++)
                        if (parts[j].Piece == PalisadeArena.Rubble)
                            Assert.GreaterOrEqual(Vector3.Distance(parts[i].Position, parts[j].Position), PalisadeArena.RubbleSpacing,
                                $"I{rung}: rubble {i} and {j} are piled");
                }
            }
        }

        [Test]
        public void NoTwoPrismsShareAPlace_AndNoneIsThin()
        {
            for (int rung = 1; rung <= 4; rung++)
            {
                Lay(rung, out var parts, out _);
                Assert.LessOrEqual(parts.Count, 120, $"I{rung} lays more than the arena's budget");
                for (int i = 0; i < parts.Count; i++)
                {
                    var s = parts[i].Scale;
                    Assert.GreaterOrEqual(Mathf.Min(s.x, Mathf.Min(s.y, s.z)), PalisadeArena.MinThickness, $"I{rung} part {i} is thin");
                    for (int j = i + 1; j < parts.Count; j++)
                    {
                        var t = parts[j].Scale;
                        // Two prisms whose centres are farther apart than their two thin sides cannot share a place
                        // when both are long the same way, which every post row and hoop is.
                        float need = 0.5f * (Mathf.Min(s.x, s.y) + Mathf.Min(t.x, t.y));
                        Assert.Greater(Vector3.Distance(parts[i].Position, parts[j].Position), need, $"I{rung} parts {i} and {j} overlap");
                    }
                }
            }
        }

        [Test]
        public void TheLanesAreNotParallel_AndStandApart()
        {
            for (int rung = 1; rung <= 4; rung++)
            {
                Lay(rung, out _, out var lanes);
                Assert.GreaterOrEqual(lanes.Count, 2, $"I{rung}: one lane is a queue, not a choice");
                for (int a = 0; a < lanes.Count; a++)
                    for (int b = a + 1; b < lanes.Count; b++)
                    {
                        Assert.Less(Mathf.Abs(Vector3.Dot(lanes[a].Axis, lanes[b].Axis)), 0.95f, $"I{rung}: lanes {a} and {b} run alike");
                        Assert.Greater(Vector3.Distance(lanes[a].FrameCentre, lanes[b].FrameCentre), 400f, $"I{rung}: frames {a} and {b} crowd");
                    }
            }
        }
    }
}

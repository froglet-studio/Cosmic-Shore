using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A probe body flown on another machine and replayed where the rings are tested (<see cref="RingProbeReplay"/>: a
    /// Wrasse kite a person rides from another machine, Docs/CREW_STATIONS.md). Driven through the ring systems' own
    /// sampler and crossing test: the path the rider flew threads the ring it crossed and no other, however late it
    /// arrives, every replayed step stays under the ring systems' step guard, and a jump threads nothing.
    /// </summary>
    [TestFixture]
    public class RingProbeReplayTests
    {
        const float Dt = 1f / 60f;
        static readonly Vector3 RingCentre = Vector3.zero, RingAxis = Vector3.forward;
        const float RingRadius = 20f;

        sealed class ReplayProbe : IRingThreadProbe
        {
            public readonly RingProbeReplay Replay = new();
            public int Frame;

            public bool TryGetRingProbe(out Vector3 position, out int epoch)
            {
                Replay.Step(Frame, Dt);
                position = Replay.Position;
                epoch = Replay.Epoch;
                return Replay.Has;
            }
        }

        static float MaxStep => RingProbeReplay.PlausibleSpeed * Dt * 2f + 5f;

        // fly the replay for frames frames; count the frames a sampled segment threads the ring's mouth, and the longest step
        static int Fly(ReplayProbe probe, int frames, out float longestStep)
        {
            var sample = new RingThreadProbeSample();
            int threads = 0;
            longestStep = 0f;
            for (int f = 0; f < frames; f++)
            {
                probe.Frame = f;
                if (!sample.Advance(probe, MaxStep * MaxStep, out var prev, out var cur)) continue;
                longestStep = Mathf.Max(longestStep, Vector3.Distance(prev, cur));
                if (RaceGateRing.PlaneCrossing(prev, cur, RingCentre, RingAxis, RingRadius) == GatePlaneCrossing.InsideMouth) threads++;
            }
            return threads;
        }

        static void Line(RingProbeReplay replay, int epoch, Vector3 from, Vector3 to, int points)
        {
            for (int k = 0; k <= points; k++) replay.Enqueue(epoch, Vector3.Lerp(from, to, k / (float)points));
        }

        [Test]
        public void ThePathTheRiderFlew_ThreadsTheRingItCrossed_Once_EvenWhenItArrivesAllAtOnce()
        {
            var probe = new ReplayProbe();
            // 200 u at 3.3 u a frame through the mouth, 5 u off its centre - all of it already queued, as after a hitch
            Line(probe.Replay, 1, new Vector3(5f, 0f, -100f), new Vector3(5f, 0f, 100f), 60);
            Assert.AreEqual(1, Fly(probe, 400, out float longest), "the ring the rider's kite crossed");
            Assert.LessOrEqual(longest, MaxStep, "a replayed step the ring systems would refuse");
        }

        [Test]
        public void APathOutsideTheMouth_ThreadsNothing()
        {
            var probe = new ReplayProbe();
            Line(probe.Replay, 1, new Vector3(50f, 0f, -100f), new Vector3(50f, 0f, 100f), 60);
            Assert.AreEqual(0, Fly(probe, 400, out _));
        }

        [Test]
        public void AFastPathFarBehind_CatchesUp_WithEveryStepUnderTheGuard_AndStillThreads()
        {
            var probe = new ReplayProbe();
            // 2 000 u at 15 u a step (900 u/s across the world: a slung kite on a boosting hull), all queued
            Line(probe.Replay, 1, new Vector3(0f, 0f, -1000f), new Vector3(0f, 0f, 1000f), 133);
            Assert.AreEqual(1, Fly(probe, 2000, out float longest));
            Assert.LessOrEqual(longest, MaxStep);
            Assert.AreEqual(0, probe.Replay.Queued, "the replay caught up");
        }

        [Test]
        public void AJumpThroughTheRing_ThreadsNothing()
        {
            var probe = new ReplayProbe();
            // the rider's kite re-placed from one side of the ring to the other (a reset): a jump, not a flight
            probe.Replay.Enqueue(1, new Vector3(0f, 0f, -200f));
            probe.Replay.Enqueue(1, new Vector3(0f, 0f, 200f));
            Assert.AreEqual(0, Fly(probe, 100, out _));
        }

        [Test]
        public void ANewSeatOnTheRidersMachine_StartsAgain_AndTheJumpThreadsNothing()
        {
            var probe = new ReplayProbe();
            probe.Replay.Enqueue(1, new Vector3(0f, 0f, -50f));
            Fly(probe, 3, out _);
            // the same path with a new epoch: the seat was bound again there; the step across the ring is not a flight
            probe.Replay.Enqueue(2, new Vector3(0f, 0f, 50f));
            var sample = new RingThreadProbeSample();
            probe.Frame = 10;
            sample.Advance(probe, MaxStep * MaxStep, out _, out _);
            Assert.AreEqual(new Vector3(0f, 0f, 50f), probe.Replay.Position);
            Assert.AreEqual(0, probe.Replay.Queued);
        }

        [Test]
        public void TheReplay_NeverRunsAhead_AndOneFrameIsOnePoint_WhoeverAsks()
        {
            var replay = new RingProbeReplay();
            replay.Step(0, Dt);
            Assert.IsFalse(replay.Has, "nothing arrived yet");
            replay.Enqueue(1, Vector3.zero);
            replay.Enqueue(1, Vector3.forward * 3f);
            replay.Step(1, Dt);
            Assert.AreEqual(Vector3.zero, replay.Position);
            replay.Step(2, Dt);
            var at = replay.Position;
            replay.Step(2, Dt);
            Assert.AreEqual(at, replay.Position, "a second ring system sampling the same frame");
            replay.Step(3, Dt);
            replay.Step(4, Dt);
            Assert.AreEqual(Vector3.forward * 3f, replay.Position, "it holds at the last point that arrived");
        }
    }
}

using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The second body a hull threads race rings with (<see cref="IRingThreadProbe"/>, the Thresher's ball):
    /// its sampler hands the ring systems a one-frame segment only when the body FLEW it - never on the first
    /// sample, across a re-seed, or on a jump - and a segment it hands over threads a ring the hull missed.
    /// </summary>
    [TestFixture]
    public class RingThreadProbeTests
    {
        sealed class FakeProbe : IRingThreadProbe
        {
            public Vector3 Position;
            public int Epoch;
            public bool Live = true;

            public bool TryGetRingProbe(out Vector3 position, out int epoch)
            {
                position = Position;
                epoch = Epoch;
                return Live;
            }
        }

        const float MaxStepSqr = 100f * 100f;

        [Test]
        public void TheFirstSample_SeedsAndTestsNothing()
        {
            var probe = new FakeProbe { Position = Vector3.zero };
            var sample = new RingThreadProbeSample();
            Assert.IsFalse(sample.Advance(probe, MaxStepSqr, out _, out _));
            probe.Position = Vector3.forward * 10f;
            Assert.IsTrue(sample.Advance(probe, MaxStepSqr, out var prev, out var cur));
            Assert.AreEqual(Vector3.zero, prev);
            Assert.AreEqual(Vector3.forward * 10f, cur);
        }

        [Test]
        public void AReSeed_IsAJump_AndThreadsNothing()
        {
            var probe = new FakeProbe();
            var sample = new RingThreadProbeSample();
            sample.Advance(probe, MaxStepSqr, out _, out _);
            probe.Position = Vector3.forward * 5f;
            probe.Epoch++;
            Assert.IsFalse(sample.Advance(probe, MaxStepSqr, out _, out _), "a ball re-placed beside a respawned hull");
            probe.Position = Vector3.forward * 10f;
            Assert.IsTrue(sample.Advance(probe, MaxStepSqr, out var prev, out _), "the frame after the re-seed flies again");
            Assert.AreEqual(Vector3.forward * 5f, prev);
        }

        [Test]
        public void ALongStep_OrNoBody_ThreadsNothing()
        {
            var probe = new FakeProbe();
            var sample = new RingThreadProbeSample();
            sample.Advance(probe, MaxStepSqr, out _, out _);
            probe.Position = Vector3.forward * 1000f;
            Assert.IsFalse(sample.Advance(probe, MaxStepSqr, out _, out _), "a step past the plausible speed is not a flight");

            probe.Live = false;
            Assert.IsFalse(sample.Advance(probe, MaxStepSqr, out _, out _));
            probe.Live = true;
            Assert.IsFalse(sample.Advance(probe, MaxStepSqr, out _, out _), "a body that comes back seeds again");
            Assert.IsFalse(sample.Advance((IRingThreadProbe)null, MaxStepSqr, out _, out _), "a hull with no second body");
        }

        [Test]
        public void Forget_DropsTheLastSample()
        {
            var probe = new FakeProbe();
            var sample = new RingThreadProbeSample();
            sample.Advance(probe, MaxStepSqr, out _, out _);
            sample.Forget();
            probe.Position = Vector3.forward;
            Assert.IsFalse(sample.Advance(probe, MaxStepSqr, out _, out _));
        }

        // The point of it: a ring the hull flies past outside the mouth is threaded by the ball the chain swings
        // through it - the same mouth test the hull's segment takes.
        [Test]
        public void TheBall_ThreadsARingTheHullMissed()
        {
            Vector3 centre = Vector3.zero, axis = Vector3.forward;
            const float radius = 20f;
            Assert.AreEqual(GatePlaneCrossing.OutsideMouth,
                RaceGateRing.PlaneCrossing(new Vector3(40f, 0f, -5f), new Vector3(40f, 0f, 5f), centre, axis, radius), "the hull");

            var probe = new FakeProbe { Position = new Vector3(5f, 0f, -5f) };
            var sample = new RingThreadProbeSample();
            sample.Advance(probe, MaxStepSqr, out _, out _);
            probe.Position = new Vector3(5f, 0f, 5f);
            Assert.IsTrue(sample.Advance(probe, MaxStepSqr, out var prev, out var cur));
            Assert.AreEqual(GatePlaneCrossing.InsideMouth, RaceGateRing.PlaneCrossing(prev, cur, centre, axis, radius), "the ball");
        }
    }
}

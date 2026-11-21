using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;
using D3 = CosmicShore.Gameplay.SolitonTunnelMath.D3;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Round 28, the Meerkat's RIFT (<c>R_VesselActions/MEERKAT.md</c>, the artifact's round 28): the path a hull flew into the
    /// void laid as one channel (<see cref="SolitonTunnelMath.PolyFrom"/>, <see cref="SolitonNetwork.LayRift"/>), standing
    /// <c>riftLife</c> s and then gone (round 28b: nothing holds it for good), and entered from its way in.
    /// </summary>
    [TestFixture]
    public class MeerkatRiftTests
    {
        SolitonTunnelConfigSO _cfg;
        readonly List<SolitonNetwork> _laid = new();

        [SetUp]
        public void SetUp() => _cfg = ScriptableObject.CreateInstance<SolitonTunnelConfigSO>();

        [TearDown]
        public void TearDown()
        {
            foreach (var n in _laid) n.Remove();
            _laid.Clear();
            Object.DestroyImmediate(_cfg);
        }

        static List<Vector3> Straight(int count, float step)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i < count; i++) pts.Add(new Vector3(i * step, 0f, 0f));
            return pts;
        }

        [Test]
        public void PolyFrom_AStraightRun_KeepsItsEndsAndLength()
        {
            var pts = new List<D3>();
            for (int i = 0; i <= 10; i++) pts.Add(new D3(i * 12, 0, 0));
            var path = SolitonTunnelMath.PolyFrom(pts);
            Assert.That(path, Is.Not.Null);
            Assert.That(path.L, Is.EqualTo(120).Within(1e-6));
            Assert.That(path.P[0].DistanceTo(new D3(0, 0, 0)), Is.LessThan(1e-9));
            Assert.That(path.P[SolitonTunnelMath.NS - 1].DistanceTo(new D3(120, 0, 0)), Is.LessThan(1e-9));
            Assert.That(path.T[0].X, Is.EqualTo(1).Within(1e-9));
            Assert.That(path.N[0].Dot(path.T[0]), Is.EqualTo(0).Within(1e-9), "the carried frame is square to the path");
        }

        [Test]
        public void PolyFrom_TooFewPointsOrNoLength_IsNull()
        {
            Assert.That(SolitonTunnelMath.PolyFrom(new List<D3> { new(1, 2, 3) }), Is.Null);
            Assert.That(SolitonTunnelMath.PolyFrom(new List<D3> { new(1, 2, 3), new(1, 2, 3) }), Is.Null);
        }

        [Test]
        public void LayRift_IsOneStandingChannelWithBothMouths()
        {
            var rift = SolitonNetwork.LayRift(_cfg, null, 7, Straight(20, 12f), 2f, 10f, 45f);
            _laid.Add(rift);
            Assert.That(rift, Is.Not.Null);
            Assert.That(rift.IsRift, Is.True);
            Assert.That(rift.Channels.Count, Is.EqualTo(1));
            var c = rift.Channels[0];
            Assert.That(c.MouthIn && c.MouthOut, Is.True);
            Assert.That(c.L, Is.EqualTo(228).Within(1e-3));
            Assert.That(rift.RiftStanding, Is.True);
            Assert.That(SolitonNetwork.Live, Has.Member(rift));
            Assert.That(SolitonTunnelMath.Radius(c, 0.5 * c.L, 0.3), Is.EqualTo(2).Within(1e-6), "no network tip taper on a rift");
        }

        [Test]
        public void LayRift_StandsItsLifeThenGoes()
        {
            const float life = 3f;
            var rift = SolitonNetwork.LayRift(_cfg, null, 8, Straight(20, 12f), 2f, 10f, life);
            _laid.Add(rift);
            double t = 0, dt = 0.05;
            while (t < life - 0.1) { Assert.That(rift.Step(dt), Is.True); t += dt; }
            Assert.That(rift.RiftStanding, Is.True, "still standing just before riftLife");
            bool alive = true;
            for (int i = 0; i < 400 && alive; i++) alive = rift.Step(dt);
            Assert.That(alive, Is.False, "gone after riftLife, its collapse and its fade");
        }

        [Test]
        public void LayRift_TooShort_LaysNothing()
        {
            Assert.That(SolitonNetwork.LayRift(_cfg, null, 9, new List<Vector3> { Vector3.zero }, 2f, 10f, 45f), Is.Null);
        }

        [Test]
        public void ARift_IsEnteredFromItsWayIn()
        {
            var rift = SolitonNetwork.LayRift(_cfg, null, 10, Straight(20, 12f), 2f, 10f, 45f);
            _laid.Add(rift);
            Assert.That(SolitonTunnelRider.AtSource(rift, new Vector3(-1f, 0f, 0f), Vector3.right), Is.True);
            Assert.That(SolitonTunnelRider.AtSource(rift, new Vector3(-1f, 0f, 0f), Vector3.left), Is.False, "facing away");
        }
    }
}

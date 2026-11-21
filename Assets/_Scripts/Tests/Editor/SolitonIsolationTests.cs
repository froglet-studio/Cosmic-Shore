using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Soliton Tunnel's rule (<see cref="SolitonIsolation"/>; Yash, 2026-11-20: "anything inside a wormhole won't
    /// interact with anything outside the hole"): two things interact only when they are in the same place, and a peer
    /// reads a rider's place from where its hull is. Each test names a way the rule could be ported wrong.
    /// </summary>
    [TestFixture]
    public class SolitonIsolationTests
    {
        SolitonTunnel _tube;
        GameObject _cargo;

        [SetUp]
        public void SetUp()
        {
            // a straight tube 200 u long from the origin along +z, throat 8, mouth 26, fully formed
            var shape = new SolitonTunnelShapeMath.Shape { L = 200f, S = 200f, R = 400f, B = 8f, M = 26f, Theta = 0f, Oval = 0f };
            _tube = SolitonTunnel.Make(null, null, Color.white, 1, Vector3.zero, Vector3.forward, Vector3.right, shape, 0.5f);
            _tube.W.Phase = SolitonTunnelShapeMath.Phase.Open;
            _tube.W.S0 = 0f;
            _tube.W.S1 = 200f;
            SolitonTunnel.Add(_tube);
            _cargo = new GameObject("SolitonIsolationTests cargo");
        }

        [TearDown]
        public void TearDown()
        {
            SolitonTunnel.Remove(_tube);
            SolitonIsolation.Unseal(_cargo);
            Object.DestroyImmediate(_cargo);
        }

        [Test]
        public void Places_AreApart_UnlessTheyAreTheSamePlace()
        {
            var other = new object();
            Assert.IsFalse(SolitonIsolation.ApartPlaces(null, null), "both in the world: they interact");
            Assert.IsFalse(SolitonIsolation.ApartPlaces(_tube, _tube), "both in the same tube: they interact");
            Assert.IsTrue(SolitonIsolation.ApartPlaces(_tube, null), "a rider and the world");
            Assert.IsTrue(SolitonIsolation.ApartPlaces(null, _tube), "...either way round");
            Assert.IsTrue(SolitonIsolation.ApartPlaces(_tube, other), "riders in different tubes");
        }

        [Test]
        public void InHollow_IsInsideTheWallAndTheLiveWindow()
        {
            Assert.IsTrue(SolitonIsolation.InHollow(100f, 0f, 200f, 7f, 8f, 0f), "inside the wall");
            Assert.IsFalse(SolitonIsolation.InHollow(100f, 0f, 200f, 9f, 8f, 0f), "outside the wall");
            Assert.IsTrue(SolitonIsolation.InHollow(100f, 0f, 200f, 9f, 8f, 2f), "the margin a peer allows a wall-hugging rider");
            Assert.IsFalse(SolitonIsolation.InHollow(100f, 120f, 200f, 1f, 8f, 4f), "before the formed window: no tube there yet");
            Assert.IsFalse(SolitonIsolation.InHollow(100f, 100f, 100f, 0f, 8f, 4f), "an unformed tube holds nothing");
            Assert.IsFalse(SolitonIsolation.InHollow(100f, 0f, 200f, 0f, 0f, 4f), "a tube with no radius holds nothing");
        }

        [Test]
        public void TubeAt_FindsTheTubeAPointIsIn()
        {
            float throat = _tube.Radius(100f);
            Assert.AreEqual(8f, throat, 0.05f, "the fixture's throat");
            Assert.AreSame(_tube, SolitonIsolation.TubeAt(new Vector3(0f, 0f, 100f), 0f), "on the centreline, mid-tube");
            Assert.AreSame(_tube, SolitonIsolation.TubeAt(new Vector3(throat - 0.5f, 0f, 100f), 0f), "pressed to the wall");
            Assert.IsNull(SolitonIsolation.TubeAt(new Vector3(throat + 6f, 0f, 100f), 4f), "past the wall and the margin");
            Assert.IsNull(SolitonIsolation.TubeAt(new Vector3(0f, 0f, 260f), 4f), "past the way out");
            Assert.IsNull(SolitonIsolation.TubeAt(new Vector3(0f, 0f, -40f), 4f), "before the way in");
            Assert.AreSame(_tube, SolitonIsolation.TubeAt(new Vector3(20f, 0f, 2f), 0f), "the mouth is wider than the throat");
        }

        [Test]
        public void TubeAt_IgnoresATubeThatIsDyingOrNotFormedThere()
        {
            var mid = new Vector3(0f, 0f, 30f);
            _tube.W.S0 = 60f;   // still forming out of its middle: nothing at s 30 yet
            Assert.IsNull(SolitonIsolation.TubeAt(mid, 4f));
            _tube.W.S0 = 0f;
            Assert.AreSame(_tube, SolitonIsolation.TubeAt(mid, 4f));
            _tube.Dying = true;
            Assert.IsNull(SolitonIsolation.TubeAt(mid, 4f), "a collapsed tube holds nobody");
            _tube.Dying = false;
        }

        [Test]
        public void Cargo_IsSealedOnlyWhileItsTubeCarriesIt()
        {
            Assert.IsFalse(SolitonIsolation.Sealed(_cargo), "in the world");
            SolitonIsolation.Seal(_cargo, _tube);
            Assert.IsTrue(SolitonIsolation.Sealed(_cargo));
            Assert.AreSame(_tube, SolitonIsolation.TubeOf(_cargo));
            Assert.IsFalse(SolitonIsolation.Idle, "cargo in a tube: the rule is live");
            SolitonIsolation.Unseal(_cargo);
            Assert.IsFalse(SolitonIsolation.Sealed(_cargo), "fired out: in the world again");
            SolitonIsolation.Seal(_cargo, null);
            Assert.IsFalse(SolitonIsolation.Sealed(_cargo), "sealed in nothing is not sealed");
        }

        [Test]
        public void AHullNobodyFliesThroughTubes_IsInTheWorld()
        {
            Assert.IsNull(SolitonIsolation.TubeOf((IVesselStatus)null));
            Assert.IsFalse(SolitonIsolation.Sealed((IVesselStatus)null));
            Assert.IsFalse(SolitonIsolation.Apart((IVesselStatus)null, (IVesselStatus)null), "two things in the world interact");
        }
    }
}

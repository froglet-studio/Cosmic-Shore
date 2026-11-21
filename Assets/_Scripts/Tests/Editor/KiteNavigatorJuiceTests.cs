#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The kite's juice (round 10, KiteNavigatorJuice): every effect is its motes' own petals, it ends on time, the Mass
    /// blocks are gone once the real prisms take over, and the charge and saved boosts wait for the petals carrying them.
    /// <c>Tools/Build/kite_navigator_lab/juice_sheet.sh</c> renders the same class frame by frame for the eye.
    /// </summary>
    public class KiteNavigatorJuiceTests
    {
        static readonly Vector3[] Square = { new(-1f, 0f, -1f), new(1f, 0f, -1f), new(1f, 0f, 1f), new(-1f, 0f, 1f) };

        static KiteNavigatorJuice Juice() =>
            new(new[] { Square, Square, Square, Square }, new[] { 0.445f, 0.395f, 0.461f, 0.461f }, new[] { 0.367f, 0.453f, 0.383f, 0.383f });

        static KiteJuiceSettings Settings() => new()
        {
            MoteSize = 4.5f, SpaceSpinDegreesPerSecond = 150f, KiteRadius = 8f, MoteSpread = 18f,
            FizzleSeconds = 0.95f, SpaceRideSeconds = 1.1f, MassMorphSeconds = 0.75f, TimeFlightSeconds = 0.7f, ChargeFlightSeconds = 0.55f,
            MassPrismScale = new Vector3(2f, 2f, 5f), PetalCount = 5, PetalSpacing = 72f,
            Elements = new[] { new Color(0.6f, 1f, 0f), new Color(0.4f, 0.8f, 0f), new Color(0.25f, 1f, 0f), new Color(0.6f, 0.8f, 0f) },
        };

        static KiteJuiceFrame Frame(float now) => new()
        {
            Now = now, ViewDepth = 400f, Origin = Vector3.zero, Up = Vector3.up, Right = Vector3.right, Forward = Vector3.forward,
            Kite = new Vector3(40f, 0f, 30f), HasHull = true, HullRotation = Quaternion.identity,
            HasPilot = true, PilotPosition = new Vector3(0f, 20f, -60f), PilotUp = Vector3.up,
        };

        static KiteBurst Burst(KiteBurstKind kind, int element, int motes)
        {
            var b = new KiteBurst { Kind = kind, X = -20f, Z = 20f, World = new Vector3(-20f, 0f, 20f), MaxScale = 240f };
            for (int i = 0; i < motes; i++) b.Motes.Add(new KiteBurstMote { X = -20f + 6f * i, Z = 20f, Spin = 40f * i, Element = element });
            if (kind == KiteBurstKind.Mass)
                for (int i = 0; i < 24; i++) b.Targets.Add((new Vector3(i % 5, i % 3, i % 4), Quaternion.Euler(10f * i, 0f, 0f)));
            return b;
        }

        static KiteNavigatorJuice Spawned(KiteBurst b)
        {
            var j = Juice();
            var list = new List<KiteBurst> { b };
            int recycled = 0;
            j.Drain(list, _ => recycled++, Frame(0f), Settings());
            Assert.AreEqual(1, recycled, "the burst goes back to the seat's pool");
            Assert.AreEqual(0, list.Count, "the burst list is drained");
            return j;
        }

        static void AssertFinite(KiteNavigatorJuice j)
        {
            for (int view = 0; view < 2; view++)
                foreach (var v in j.Vertices(view))
                    Assert.IsTrue(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z), "a juice vertex is not finite");
        }

        [Test]
        public void EveryEffect_Draws_StaysFinite_AndEnds()
        {
            var kinds = new[] { (KiteBurstKind.Fizzle, 1, 0.95f), (KiteBurstKind.Space, 2, 1.3f), (KiteBurstKind.Mass, 1, 1.1f),
                                (KiteBurstKind.Time, 3, 1.5f), (KiteBurstKind.Charge, 0, 1.6f), (KiteBurstKind.Pop, 0, 0.5f),
                                (KiteBurstKind.Steal, 0, 0.9f), (KiteBurstKind.Bloom, 2, 0.85f) };
            foreach (var (kind, element, life) in kinds)
            {
                var b = Burst(kind, element, kind is KiteBurstKind.Pop or KiteBurstKind.Steal or KiteBurstKind.Bloom ? 0 : 3);
                b.Element = element;
                var j = Spawned(b);
                Assert.Greater(j.Count, 0, $"{kind} spawned nothing");
                j.Build(Frame(0.05f), Settings());
                Assert.Greater(j.Triangles(0).Count, 0, $"{kind} draws nothing in the kite view");
                Assert.Greater(j.Triangles(1).Count, 0, $"{kind} draws nothing in the pilot view");
                for (float t = 0.1f; t < life; t += 0.1f)
                {
                    j.Build(Frame(t), Settings());
                    AssertFinite(j);
                }
                j.Build(Frame(life + 0.3f), Settings());
                Assert.AreEqual(0, j.Count, $"{kind} outlives its effect");
            }
        }

        [Test]
        public void Mass_BecomesBlocks_ThatAreGoneWhenThePrismsTakeOver()
        {
            var j = Spawned(Burst(KiteBurstKind.Mass, 1, 3));
            Assert.AreEqual(24, j.Count, "one block per prism (the motes' 15 petals fold into them)");
            j.Build(Frame(0.5f), Settings());
            Assert.Greater(j.Triangles(0).Count, 24 * 12 - 1, "every block is a solid of six faces mid-flight");
            j.Build(Frame(0.75f + 0.3f + 0.06f), Settings());
            Assert.AreEqual(0, j.Count, "the blocks outlast the prisms being laid");
        }

        [Test]
        public void Fizzle_HasNoRing_ItsPetalsSputterOut()
        {
            var j = Spawned(Burst(KiteBurstKind.Fizzle, 1, 2));
            Assert.AreEqual(10, j.Count, "five petals a mote, nothing else (no ring)");
        }

        [Test]
        public void ChargeAndBoosts_WaitForTheirPetals()
        {
            var j = Juice();
            j.ShownCharge(0f, 0f);
            j.ShownBoosts(0, 0f);
            var list = new List<KiteBurst> { Burst(KiteBurstKind.Charge, 0, 3), Burst(KiteBurstKind.Time, 3, 3) };
            j.Drain(list, _ => { }, Frame(0f), Settings());
            Assert.AreEqual(0f, j.ShownCharge(4f, 0.1f), "the pips show before the charge has reached the Wrasse");
            Assert.AreEqual(0, j.ShownBoosts(2, 0.1f), "the boosts show before the Time has reached the Grouper");
            Assert.AreEqual(4f, j.ShownCharge(4f, 3f));
            Assert.AreEqual(2, j.ShownBoosts(2, 3f));
            Assert.AreEqual(1f, j.ShownCharge(1f, 3.1f), "spending charge shows at once");
        }
    }
}
#endif

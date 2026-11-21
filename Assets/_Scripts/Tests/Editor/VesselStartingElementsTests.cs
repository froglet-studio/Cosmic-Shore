#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The per-hull starting-element table's two contracts: resolution (an intensity row wins
    /// over the every-intensity row, and a hull with no row is NOT seeded) and the wire round
    /// trip (what the config RPC packs is exactly what a client unpacks).
    /// </summary>
    public class VesselStartingElementsTests
    {
        static ResourceCollection Levels(float m, float c, float s, float t) => new(m, c, s, t);

        [Test]
        public void Resolve_PrefersTheIntensityRow_OverTheEveryIntensityRow()
        {
            var table = new List<VesselStartingElements>
            {
                new(VesselClassType.Manta, 0, Levels(0f, 0f, 0f, -0.3f)),
                new(VesselClassType.Manta, 4, Levels(0f, 0f, 0f, 0.2f)),
            };

            Assert.IsTrue(VesselStartingElements.TryResolve(table, VesselClassType.Manta, 1, out var atOne));
            Assert.AreEqual(-0.3f, atOne.Time, 1e-6f);
            Assert.IsTrue(VesselStartingElements.TryResolve(table, VesselClassType.Manta, 4, out var atFour));
            Assert.AreEqual(0.2f, atFour.Time, 1e-6f);
        }

        [Test]
        public void Resolve_OrderIndependent_AndUnlistedHullIsNotSeeded()
        {
            var reversed = new List<VesselStartingElements>
            {
                new(VesselClassType.Sparrow, 2, Levels(0f, 0f, 0f, 1f)),
                new(VesselClassType.Sparrow, 0, Levels(0f, 0f, 0f, 0.5f)),
            };
            Assert.IsTrue(VesselStartingElements.TryResolve(reversed, VesselClassType.Sparrow, 2, out var r));
            Assert.AreEqual(1f, r.Time, 1e-6f);

            Assert.IsFalse(VesselStartingElements.TryResolve(reversed, VesselClassType.Rhino, 2, out _),
                "a hull with no row must be left at rest, never handed zeros");
            Assert.IsFalse(VesselStartingElements.TryResolve(null, VesselClassType.Rhino, 2, out _));
        }

        [Test]
        public void PackUnpack_RoundTripsEveryRow()
        {
            var table = new List<VesselStartingElements>
            {
                new(VesselClassType.Urchin, 0, Levels(0.1f, 0.2f, 0.3f, 0.4f)),
                new(VesselClassType.Scarab, 3, Levels(-0.5f, 0f, 1f, 1.5f)),
            };

            VesselStartingElements.Pack(table, out var classes, out var intensities, out var levels);
            Assert.AreEqual(2, classes.Length);
            Assert.AreEqual(8, levels.Length);

            var back = new List<VesselStartingElements>();
            VesselStartingElements.Unpack(classes, intensities, levels, back);

            Assert.AreEqual(table.Count, back.Count);
            for (int i = 0; i < table.Count; i++)
            {
                Assert.AreEqual(table[i].Class, back[i].Class);
                Assert.AreEqual(table[i].Intensity, back[i].Intensity);
                Assert.AreEqual(table[i].Levels.Mass, back[i].Levels.Mass, 1e-6f);
                Assert.AreEqual(table[i].Levels.Charge, back[i].Levels.Charge, 1e-6f);
                Assert.AreEqual(table[i].Levels.Space, back[i].Levels.Space, 1e-6f);
                Assert.AreEqual(table[i].Levels.Time, back[i].Levels.Time, 1e-6f);
            }
        }

        [Test]
        public void Unpack_DropsATornTable_RatherThanGuessing()
        {
            var into = new List<VesselStartingElements> { new(VesselClassType.Manta, 0, default) };
            VesselStartingElements.Unpack(new[] { 1, 2 }, new[] { 0 }, new float[] { 0, 0, 0, 0 }, into);
            Assert.AreEqual(1, into.Count, "only the rows every array agrees on survive");
            VesselStartingElements.Unpack(null, null, null, into);
            Assert.AreEqual(0, into.Count);
        }
    }
}
#endif

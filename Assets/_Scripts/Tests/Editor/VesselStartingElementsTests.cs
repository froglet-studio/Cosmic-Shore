#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The per-hull starting-element table's three contracts: resolution (a row naming a hull
    /// beats an Any wildcard, an intensity row beats an every-intensity row, and a hull no row
    /// reaches is NOT seeded), the intensity-1 baseline a card without its own table is published
    /// with, and the wire round trip (what the config RPC packs is exactly what a client unpacks).
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
        public void Baseline_SeedsEveryHullAtLevelFive_OnIntensityOneOnly()
        {
            var published = new List<VesselStartingElements>();
            VesselStartingElements.BuildPublishedTable(new List<VesselStartingElements>(), published);

            foreach (var hull in new[]
                     {
                         VesselClassType.Manta, VesselClassType.Dolphin, VesselClassType.Rhino,
                         VesselClassType.Urchin, VesselClassType.Grizzly, VesselClassType.Squirrel,
                         VesselClassType.Serpent, VesselClassType.Termite, VesselClassType.Falcon,
                         VesselClassType.Shrike, VesselClassType.Sparrow, VesselClassType.Scarab,
                     })
            {
                Assert.IsTrue(VesselStartingElements.TryResolve(published, hull, 1, out var at1),
                    $"{hull} must be seeded on intensity 1");
                Assert.AreEqual(0.5f, at1.Mass, 1e-6f);
                Assert.AreEqual(0.5f, at1.Charge, 1e-6f);
                Assert.AreEqual(0.5f, at1.Space, 1e-6f);
                Assert.AreEqual(0.5f, at1.Time, 1e-6f);

                for (int intensity = 2; intensity <= 4; intensity++)
                    Assert.IsFalse(VesselStartingElements.TryResolve(published, hull, intensity, out _),
                        $"{hull} must stay at rest on intensity {intensity}");
            }
        }

        [Test]
        public void Baseline_IsNotAddedToACardThatAuthorsItsOwnTable()
        {
            // Regatta/Broadside shape: a handicap row for ONE hull, every other hull deliberately
            // left at rest. A per-hull baseline would seed exactly those anchor hulls and destroy
            // the spread the balance model solved for, so an authored table gets no baseline.
            var authored = new List<VesselStartingElements>
            {
                new(VesselClassType.Manta, 1, Levels(0f, 0f, 0f, -0.5f)),
            };
            var published = new List<VesselStartingElements>();
            VesselStartingElements.BuildPublishedTable(authored, published);

            Assert.AreEqual(1, published.Count, "an authored table is published verbatim");
            Assert.IsTrue(VesselStartingElements.TryResolve(published, VesselClassType.Manta, 1, out var manta));
            Assert.AreEqual(-0.5f, manta.Time, 1e-6f);
            Assert.IsFalse(VesselStartingElements.TryResolve(published, VesselClassType.Rhino, 1, out _),
                "an anchor hull keeps its authored rest levels");
        }

        [Test]
        public void Resolve_NamingAHullBeatsTheAnyWildcard()
        {
            var table = new List<VesselStartingElements>
            {
                new(VesselClassType.Any, 1, Levels(0.5f, 0.5f, 0.5f, 0.5f)),
                new(VesselClassType.Sparrow, 1, Levels(0f, 0f, 0f, 1f)),
                new(VesselClassType.Scarab, 0, Levels(0f, 0f, 0f, 0.25f)),
            };

            Assert.IsTrue(VesselStartingElements.TryResolve(table, VesselClassType.Sparrow, 1, out var sparrow));
            Assert.AreEqual(1f, sparrow.Time, 1e-6f, "the hull row wins over the wildcard");

            Assert.IsTrue(VesselStartingElements.TryResolve(table, VesselClassType.Scarab, 1, out var scarab));
            Assert.AreEqual(0.25f, scarab.Time, 1e-6f,
                "class specificity dominates intensity specificity");

            Assert.IsTrue(VesselStartingElements.TryResolve(table, VesselClassType.Rhino, 1, out var rhino));
            Assert.AreEqual(0.5f, rhino.Time, 1e-6f, "an unnamed hull falls through to the wildcard");

            Assert.IsFalse(VesselStartingElements.TryResolve(table, VesselClassType.Rhino, 2, out _),
                "the wildcard row names intensity 1 and must not reach intensity 2");
        }

        [Test]
        public void Resolve_RandomIsNotAWildcard()
        {
            // Random (0) is the default-constructed Class. A row nobody authored must not seed
            // every hull in the match.
            var table = new List<VesselStartingElements> { new(VesselClassType.Random, 1, Levels(1f, 1f, 1f, 1f)) };
            Assert.IsFalse(VesselStartingElements.TryResolve(table, VesselClassType.Manta, 1, out _));
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

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The shipped roster that Tools/Build/author_pilot_assets.py writes (Garrett, 2026-10-27): eighteen pilots, one per
    /// callsign, on any vessel (Docs/Pilots/CALLSIGNS.md); Javelin and Aegis, payload transformations with four element
    /// tracks each and a twist at level 5 (Docs/Pilots/ARTIFACTS.md); the track numbers both transformations read; the
    /// spend rule (<see cref="PilotLoadoutStore.Price"/>) and the Hangar's slot rule (<see cref="PilotLoadoutStore.Toggle"/>).
    /// <see cref="PilotCatalogAssetTests"/> holds the catalog's own validation.
    /// </summary>
    public class PilotsAndArtifactsTests
    {
        const string CatalogPath = "Assets/Resources/Pilots/PilotArtifactCatalog.asset";

        static PilotArtifactCatalogSO Catalog()
        {
            var c = AssetDatabase.LoadAssetAtPath<PilotArtifactCatalogSO>(CatalogPath);
            Assert.IsNotNull(c, $"no catalog at {CatalogPath}: run python3 Tools/Build/author_pilot_assets.py");
            return c;
        }

        static ArtifactSO Artifact(string id)
        {
            Assert.IsTrue(Catalog().TryGetArtifact(id, out var a), $"no artifact {id} in the catalog");
            return a;
        }

        [Test]
        public void Roster_OnePilotPerCallsign_NamedForIt_OnAnyVessel()
        {
            var set = PilotSight.Set;
            Assert.IsNotNull(set, "no callsign set");
            var pilots = Catalog().Pilots.Where(p => p).ToList();
            var visions = pilots.Select(p => p.Vision).ToList();
            CollectionAssert.AllItemsAreUnique(visions, "two pilots carry one callsign");
            foreach (PilotVision v in System.Enum.GetValues(typeof(PilotVision)))
                if (v != PilotVision.None) CollectionAssert.Contains(visions, v, $"no pilot carries {v}");
            foreach (var p in pilots)
            {
                Assert.AreEqual(set.For(p.Vision)?.word, p.DisplayName, $"{p.name} is not named for its callsign");
                Assert.AreEqual(VesselClassType.Any, p.Requirement.Vessel, $"{p.name} is bound to a vessel");
                CollectionAssert.IsEmpty(p.Requirement.Keywords ?? new List<VesselKeyword>(), $"{p.name} requires keywords");
            }
        }

        [Test]
        public void Roster_JavelinAndAegis_AreTransformationsBoundToNoClass_OneSlotEach()
        {
            var catalog = Catalog();
            Assert.AreEqual(1, catalog.SlotsPerVessel, "one artifact slot per squad slot (ARTIFACTS.md §2)");
            Assert.IsInstanceOf<JavelinTransformSO>(Artifact("artifact.javelin").Transform);
            Assert.IsInstanceOf<AegisTransformSO>(Artifact("artifact.aegis").Transform);
            foreach (var a in catalog.Artifacts)
            {
                Assert.AreEqual(VesselClassType.Any, a.Requirement.Vessel, $"{a.name}: no vessel class appears in an artifact");
                Assert.IsNotEmpty(a.Description, a.name);
                foreach (var e in new[] { Element.Mass, Element.Space, Element.Time, Element.Charge })
                    Assert.IsNotNull(a.Track(e), $"{a.name} has no {e} track");
            }
        }

        [Test]
        public void Track_ValueLerpsZeroToTen_AndEachLevelCostsMoreUntilTheTop()
        {
            var t = new ArtifactTrack { atZero = 4f, atTen = 16f, baseCost = 2, costStep = 1 };
            Assert.AreEqual(4f, t.Value(0), 1e-4f);
            Assert.AreEqual(10f, t.Value(5), 1e-4f);
            Assert.AreEqual(16f, t.Value(10), 1e-4f);
            Assert.AreEqual(16f, t.Value(14), 1e-4f, "clamped at the top");
            Assert.AreEqual(2, t.CostFrom(0));
            Assert.AreEqual(11, t.CostFrom(9));
            Assert.AreEqual(0, t.CostFrom(10), "nothing to raise at the top");
            Assert.IsFalse(ArtifactSO.Twisted(Element.Mass, new ArtifactLevels(4, 0, 0, 0)));
            Assert.IsTrue(ArtifactSO.Twisted(Element.Mass, new ArtifactLevels(5, 0, 0, 0)), "the twist is at level 5");
            Assert.AreEqual(10, new ArtifactLevels(13, -2, 0, 0).Mass, "levels clamp to 0-10");
            Assert.AreEqual(0, new ArtifactLevels(13, -2, 0, 0).Space);
        }

        [Test]
        public void Javelin_ReadsItsTracks_AndTwistsAtFive()
        {
            var javelin = Artifact("artifact.javelin");
            var so = (JavelinTransformSO)javelin.Transform;

            var plain = so.Shot(javelin, new ArtifactLevels(0, 0, 0, 0));
            Assert.AreEqual(4f, plain.Width, 1e-3f);
            Assert.AreEqual(150f, plain.Range, 1e-3f);
            Assert.AreEqual(0f, plain.Linger, 1e-3f);
            Assert.AreEqual(1, plain.Cracks);
            Assert.IsFalse(plain.WideBurst || plain.Pierces || plain.LineLingers || plain.AlsoShielded, "no twist at level 0");
            Assert.IsTrue(plain.StopsAfter(1), "stops at its first crack");

            var top = so.Shot(javelin, new ArtifactLevels(10, 10, 10, 10));
            Assert.AreEqual(16f, top.Width, 1e-3f);
            Assert.AreEqual(500f, top.Range, 1e-3f);
            Assert.AreEqual(6, top.Cracks);
            Assert.IsTrue(top.WideBurst && top.Pierces && top.LineLingers && top.AlsoShielded);
            Assert.IsFalse(top.StopsAfter(100), "a piercing Javelin never stops for what it cracks");
        }

        [Test]
        public void Aegis_ReadsItsTracks_AndReachesHomeOnlyWhenNear()
        {
            var aegis = Artifact("artifact.aegis");
            var so = (AegisTransformSO)aegis.Transform;
            Assert.IsNotEmpty(so.RefusedLine, "a refused Aegis says why (authored)");

            var plain = so.Ward(aegis, new ArtifactLevels(0, 0, 0, 0));
            Assert.AreEqual(8f, plain.Seconds, 1e-3f);
            Assert.AreEqual(0, plain.Reraises);
            Assert.AreEqual(30, plain.Covers(100), "30% of the hull at level 0");
            Assert.IsTrue(plain.Reaches(120f));
            Assert.IsFalse(plain.Reaches(121f), "released too far from the Ark");

            var top = so.Ward(aegis, new ArtifactLevels(10, 10, 10, 10));
            Assert.AreEqual(100, top.Covers(100), "the Mass twist covers the whole hull");
            Assert.IsTrue(top.Reaches(10000f), "the Space twist reaches home from anywhere");
            Assert.AreEqual(40f, top.Seconds, 1e-3f);
            Assert.AreEqual(3, top.Reraises);
            Assert.IsTrue(top.LeavesShield && top.ReraisesAtOnce);
        }

        [Test]
        public void Price_PaysTheNextLevel_RefusesShortBalances_AndStopsAtTheTop()
        {
            var t = new ArtifactTrack { baseCost = 2, costStep = 1 };
            Assert.AreEqual(PilotLoadoutStore.RaiseResult.Raised, PilotLoadoutStore.Price(t, 3, 5, out int cost));
            Assert.AreEqual(5, cost);
            Assert.AreEqual(PilotLoadoutStore.RaiseResult.NotEnoughCrystals, PilotLoadoutStore.Price(t, 3, 4, out _));
            Assert.AreEqual(PilotLoadoutStore.RaiseResult.AtTop, PilotLoadoutStore.Price(t, ArtifactTrack.MaxLevel, 999, out _));
            Assert.AreEqual(PilotLoadoutStore.RaiseResult.NoSuchTrack, PilotLoadoutStore.Price(null, 0, 999, out _));
        }

        [Test]
        public void Toggle_SlotsUpToTheCountAndTakesOut()
        {
            var slotted = new List<string>();
            Assert.IsTrue(PilotLoadoutStore.Toggle(slotted, "A", 2, true));
            Assert.IsTrue(PilotLoadoutStore.Toggle(slotted, "B", 2, true));
            Assert.IsFalse(PilotLoadoutStore.Toggle(slotted, "C", 2, true), "no third slot");
            CollectionAssert.AreEqual(new[] { "A", "B" }, slotted);
            Assert.IsFalse(PilotLoadoutStore.Toggle(slotted, "A", 2, true), "a slotted artifact comes out");
            CollectionAssert.AreEqual(new[] { "B" }, slotted);
            Assert.IsFalse(PilotLoadoutStore.Toggle(slotted, "D", 2, false), "an artifact not held never goes in");
            Assert.IsFalse(PilotLoadoutStore.Toggle(slotted, "", 2, true));
            CollectionAssert.AreEqual(new[] { "B" }, slotted);
        }
    }
}
#endif

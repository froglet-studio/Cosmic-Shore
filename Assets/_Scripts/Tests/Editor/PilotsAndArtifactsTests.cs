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
    /// starter hoard, every kind rolling unique artifacts (Docs/Pilots/INVENTORY.md), the feed rule
    /// (<see cref="PilotLoadoutStore.Check"/>) and the Hangar's slot rule (<see cref="PilotLoadoutStore.Toggle"/>). The
    /// inventory's own rules are <see cref="AccountInventoryTests"/>.
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
            Assert.IsInstanceOf<HydraTransformSO>(Artifact("artifact.hydra").Transform);
            Assert.IsInstanceOf<AftershockTransformSO>(Artifact("artifact.aftershock").Transform);
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
            Assert.AreEqual(10f, new ArtifactLevels(13, -2, 0, 0).Mass, "levels clamp to 0-10");
            Assert.AreEqual(0f, new ArtifactLevels(13, -2, 0, 0).Space);
            Assert.AreEqual(7f, t.Value(2.5f), 1e-4f, "levels are continuous since 2026-11-07");
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
        public void Hydra_ReadsItsTracks_FansItsHeads_AndTwistsAtFive()
        {
            var hydra = Artifact("artifact.hydra");
            var so = (HydraTransformSO)hydra.Transform;

            var plain = so.Shot(hydra, new ArtifactLevels(0, 0, 0, 0));
            Assert.AreEqual(3, plain.Heads, "three heads at level 0");
            Assert.AreEqual(14f, plain.Radius, 1e-3f);
            Assert.AreEqual(120f, plain.Range, 1e-3f);
            Assert.AreEqual(0f, plain.Linger, 1e-3f);
            Assert.IsFalse(plain.CracksInFlight || plain.Seeks || plain.LingerGrows || plain.SplitsAgain, "no twist at level 0");
            Assert.AreEqual(plain.Radius, plain.LingerRadius(1f), 1e-3f);

            var top = so.Shot(hydra, new ArtifactLevels(10, 10, 10, 10));
            Assert.AreEqual(6, top.Heads);
            Assert.IsTrue(top.CracksInFlight && top.Seeks && top.LingerGrows && top.SplitsAgain);
            Assert.AreEqual(top.Radius * 2f, top.LingerRadius(1f), 1e-3f, "the Time twist ends twice as wide");

            var left = HydraTransformSO.HeadDirection(Vector3.forward, Vector3.up, 0, 3, 50f);
            var mid = HydraTransformSO.HeadDirection(Vector3.forward, Vector3.up, 1, 3, 50f);
            var right = HydraTransformSO.HeadDirection(Vector3.forward, Vector3.up, 2, 3, 50f);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, mid), 1e-2f, "the middle head flies straight on");
            Assert.AreEqual(50f, Vector3.Angle(left, right), 1e-2f, "the outer heads span the fan");
            Assert.AreEqual(0f, left.y, 1e-4f, "the fan lies across the heading, level");
        }

        [Test]
        public void Aftershock_ReadsItsTracks_AndTwistsAtFive()
        {
            var aftershock = Artifact("artifact.aftershock");
            var so = (AftershockTransformSO)aftershock.Transform;

            var plain = so.Shot(aftershock, new ArtifactLevels(0, 0, 0, 0));
            Assert.AreEqual(2, plain.Shocks, "it cracks twice at level 0");
            Assert.AreEqual(5f, plain.Interval, 1e-3f);
            Assert.AreEqual(0f, plain.Throw, 1e-3f, "lands where it is released at level 0");
            Assert.AreEqual(plain.Radius, plain.RadiusOf(1), 1e-3f);
            Assert.IsFalse(plain.LastReachesFar || plain.Rings || plain.Shatters);

            var top = so.Shot(aftershock, new ArtifactLevels(10, 10, 10, 10));
            Assert.AreEqual(5, top.Shocks, "four shocks, and the Time twist adds one more");
            Assert.AreEqual(2f, top.Interval, 1e-3f, "Time shortens the wait between shocks");
            Assert.AreEqual(240f, top.Throw, 1e-3f);
            Assert.AreEqual(top.Radius * 2f, top.RadiusOf(top.Shocks - 1), 1e-3f, "the Mass twist: the last reaches twice as far");
            Assert.AreEqual(top.Radius, top.RadiusOf(0), 1e-3f);
            Assert.IsTrue(top.Rings && top.Shatters);
        }

        [Test]
        public void VoyageFinds_AreNotInTheStarterHoard_TheRestAre()
        {
            Assert.IsTrue(Artifact("artifact.hydra").FoundOnVoyages);
            Assert.IsTrue(Artifact("artifact.aftershock").FoundOnVoyages);
            Assert.IsFalse(Artifact("artifact.javelin").FoundOnVoyages);
            Assert.IsFalse(Artifact("artifact.aegis").FoundOnVoyages);

            // A fresh account's hoard (AccountInventoryStore.GrantStarter): one unique artifact of each free kind, none of
            // a kind found only on voyages, and one pilot per callsign.
            var inv = new AccountInventory();
            AccountInventoryStore.GrantStarter(inv, Catalog(), 0);
            Assert.AreEqual(1, inv.CountOfKind("artifact.javelin"));
            Assert.AreEqual(1, inv.CountOfKind("artifact.aegis"));
            Assert.AreEqual(0, inv.CountOfKind("artifact.hydra"), "found on voyages: not a starter");
            Assert.AreEqual(Callsigns.All.Count, inv.Pilots.Count);
            Assert.IsTrue(inv.ResolveArtifact("artifact.javelin") != null, "a legacy kind id resolves to the starter Javelin");
        }

        [Test]
        public void EveryKind_RollsUniqueArtifacts_AttunedToOneOfItsTracks()
        {
            foreach (var kindSO in Catalog().Artifacts)
            {
                var kind = kindSO.ToArchetype();
                var ids = new HashSet<string>();
                for (ulong seed = 1; seed <= 64; seed++)
                {
                    var a = ArtifactGenerator.Roll(kind, seed);
                    Assert.IsNotNull(a, kindSO.Id);
                    Assert.IsTrue(ids.Add(a.Id));
                    Assert.IsNotNull(kindSO.Track(a.Element), $"{a.Id} is attuned to a track {kindSO.Id} authors");
                    Assert.IsTrue(Catalog().TryGetArtifact(a.Id, out var back) && back == kindSO, "the catalog finds the kind from the id");
                }
            }
        }

        [Test]
        public void Feed_WithNoProfileLoaded_RefusesAndLeavesTheArtifact()
        {
            Assume.That(!CosmicShore.UI.PlayerDataService.Instance, "a profile is loaded in this editor session");
            var a = ArtifactGenerator.Roll(Artifact("artifact.javelin").ToArchetype(), 7);
            Assert.AreEqual(0, PilotLoadoutStore.Crystals(a.Element), "no profile, no crystals");
            Assert.AreEqual(PilotLoadoutStore.FeedOutcome.NotEnoughCrystals, PilotLoadoutStore.Check(a, a.Element, 5, 0));
            Assert.AreEqual(PilotLoadoutStore.FeedOutcome.Fed, PilotLoadoutStore.Check(a, a.Element, 5, 5));
            Assert.AreEqual(PilotLoadoutStore.FeedOutcome.Fed, PilotLoadoutStore.Check(a, Element.Omni, 5, 5), "omni feeds any artifact");
            var other = a.Element == Element.Mass ? Element.Space : Element.Mass;
            Assert.AreEqual(PilotLoadoutStore.FeedOutcome.WrongElement, PilotLoadoutStore.Check(a, other, 5, 99), "one element only");
            Assert.AreEqual(PilotLoadoutStore.FeedOutcome.NothingToFeed, PilotLoadoutStore.Check(a, a.Element, 0, 99));
            Assert.AreEqual(PilotLoadoutStore.FeedOutcome.NoSuchArtifact, PilotLoadoutStore.Check(null, a.Element, 5, 99));
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

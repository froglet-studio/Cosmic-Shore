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
    /// The 2026-10-26 roster (Docs/Pilots/PILOTS_AND_ARTIFACTS.md) on the shipped catalog that
    /// Tools/Build/author_pilot_assets.py writes: two pilots and two artifacts per Hangar vessel, the fleet's five, how an
    /// artifact's rank value applies to its stat, and the Hangar's slot rule (<see cref="PilotLoadoutStore.Toggle"/>).
    /// <see cref="PilotCatalogAssetTests"/> holds the default pilots and the catalog's own validation.
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
        public void Roster_EveryHangarVesselHasTwoPilotsAndTwoArtifacts_ThenTheFleets()
        {
            var catalog = Catalog();
            var vessels = catalog.Pilots.Where(p => p && !p.IsDefault).Select(p => p.Requirement.Vessel).Distinct().ToList();
            Assert.AreEqual(20, vessels.Count, "the 20 Hangar vessels each have pilots");
            var pilots = new List<PilotSO>();
            var artifacts = new List<ArtifactSO>();
            foreach (var v in vessels)
            {
                catalog.GetPilotsFor(v, null, pilots);
                Assert.AreEqual(2, pilots.Count(p => !p.IsDefault), $"{v} has two pilots besides its default");
                catalog.GetArtifactsFor(v, null, artifacts);
                Assert.AreEqual(2, artifacts.Count(a => !a.IsFleet), $"{v} has its own two artifacts");
                Assert.AreEqual(5, artifacts.Count(a => a.IsFleet), $"{v} is offered the fleet's five");
                Assert.IsFalse(artifacts.Take(2).Any(a => a.IsFleet), $"{v}: its own artifacts are listed first");
            }
            Assert.AreEqual(3, catalog.SlotsPerVessel, "three artifact slots per vessel (Shombith, 2026-10-26)");
        }

        [Test]
        public void Roster_EveryPilotSaysWhatItRewrites_AndIsNotTiedToAnElement()
        {
            foreach (var p in Catalog().Pilots.Where(p => p && !p.IsDefault))
            {
                Assert.IsNotEmpty(p.Ability, p.name);
                Assert.IsNotEmpty(p.Description, p.name);
                Assert.IsNotEmpty(p.AIBehaviorDescription, p.name);
                Assert.IsTrue(p.ElementalBoost.IsNone(), $"{p.name}: pilots are not tied to the elements");
                Assert.IsFalse(p.PortraitIsStandIn && !p.Portrait, $"{p.name}: a stand-in with no portrait");
            }
        }

        [Test]
        public void Apply_EachKindMovesTheBaseValue()
        {
            Assert.AreEqual(5f, Artifact("artifact.scarab.fourth-switch").Apply(3f, 3), 1e-4f, "Set: 3 standing switches become 5 at rank III");
            Assert.AreEqual(4f, Artifact("artifact.sparrow.extra-bay").Apply(3f, 1), 1e-4f, "Add: one more missile at rank I");
            Assert.AreEqual(2f, Artifact("artifact.fleet.comet-shard").Apply(1f, 3), 1e-4f, "Multiply: crystal effects x2 at rank III");
            Assert.AreEqual(130f, Artifact("artifact.sparrow.long-barrel").Apply(100f, 3), 1e-3f, "Percent: +30% at rank III");
            Assert.AreEqual(1.6f, Artifact("artifact.shrike.quick-dock").Apply(1.6f, 0), 1e-4f, "rank 0 (not found) leaves the base");
            Assert.AreEqual(1.25f, Artifact("artifact.shrike.quick-dock").Apply(1.6f, 3), 1e-4f, "the docked penalty becomes x1.25 at rank III");
            Assert.AreEqual("5 pellets", Artifact("artifact.serpent.fifth-pellet").RankLabel(1));
            Assert.AreEqual(string.Empty, Artifact("artifact.serpent.fifth-pellet").RankLabel(0));
        }

        [Test]
        public void Tomahawk_LoopLeavesAndComesHome_WidestBesideTheHull()
        {
            Assert.IsTrue(Catalog().TryGetPilot(PilotIds.Tomahawk, out var tomahawk), "Tomahawk is in the catalog");
            var cfg = tomahawk.Tuning as TomahawkThrowConfigSO;
            Assert.IsNotNull(cfg, "Tomahawk's tuning is its throw config");
            const float blade = 30f;
            Assert.Less(cfg.LoopOffset(0f, 1, blade).magnitude, 1e-3f, "leaves from the mount");
            Assert.Less(cfg.LoopOffset(1f, 1, blade).magnitude, 1e-3f, "comes home");
            var right = cfg.LoopOffset(0.5f, 1, blade);
            var left = cfg.LoopOffset(0.5f, -1, blade);
            Assert.AreEqual(cfg.SideReachBlades * blade, right.x, 1e-3f, "widest to the right at half way");
            Assert.AreEqual(-right.x, left.x, 1e-3f, "a left throw mirrors it");
            Assert.Greater(cfg.LoopOffset(0.25f, 1, blade).z, 0f, "out ahead first");
            Assert.Less(cfg.LoopOffset(0.75f, 1, blade).z, 0f, "home from behind");
            Assert.AreEqual(1, cfg.SideOf("RR"));
            Assert.AreEqual(-1, cfg.SideOf("LL"));
            Assert.AreEqual(0, cfg.SideOf("RL"), "a mixed string keeps its flourish");
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
            Assert.IsFalse(PilotLoadoutStore.Toggle(slotted, "D", 2, false), "an artifact not found never goes in");
            Assert.IsFalse(PilotLoadoutStore.Toggle(slotted, "", 2, true));
            CollectionAssert.AreEqual(new[] { "B" }, slotted);
        }
    }
}
#endif

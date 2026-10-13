#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The SHIPPED pilot assets (Docs/PILOTS_AND_ARTIFACTS.md §10, slice 2), as authored by
    /// Tools/Build/author_pilot_assets.py: the catalog validates clean, and every milestone vessel
    /// declares its keywords and resolves to its own default pilot, which its picker lists first.
    /// A vessel with no default pilot would be offered nothing and resolve to null at match start.
    /// </summary>
    [TestFixture]
    public class PilotCatalogAssetTests
    {
        const string CatalogPath = "Assets/_SO_Assets/Pilots/PilotArtifactCatalog.asset";

        static readonly string[] MilestoneVessels =
            { "Manta", "Dolphin", "Rhino", "Squirrel", "Serpent", "Sparrow", "Urchin", "Butterfly", "Thresher" };

        static PilotArtifactCatalogSO LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PilotArtifactCatalogSO>(CatalogPath);
            Assert.IsNotNull(catalog, $"{CatalogPath} is missing; run Tools/Build/author_pilot_assets.py");
            return catalog;
        }

        static SO_Vessel LoadVessel(string vessel)
        {
            string path = $"Assets/_SO_Assets/Classes/SO_Class_{vessel}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<SO_Vessel>(path);
            Assert.IsNotNull(asset, $"{path} is missing");
            return asset;
        }

        [Test]
        public void Catalog_ValidatesClean()
        {
            var problems = new List<string>();
            LoadCatalog().Validate(problems);
            CollectionAssert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EveryMilestoneVessel_DeclaresKeywords_IncludingBasePrismProduction()
        {
            foreach (var name in MilestoneVessels)
            {
                var vessel = LoadVessel(name);
                CollectionAssert.Contains(vessel.Keywords, VesselKeyword.BasePrismProduction,
                    $"{name} declares no BasePrismProduction; the whole milestone fleet lays mass");
            }
        }

        [Test]
        public void EveryMilestoneVessel_ResolvesToItsOwnDefaultPilot_ListedFirst()
        {
            var catalog = LoadCatalog();
            var offered = new List<PilotSO>();

            foreach (var name in MilestoneVessels)
            {
                var vessel = LoadVessel(name);
                Assert.IsTrue(catalog.TryGetDefaultPilot(vessel.Class, out var pilot), $"{name} has no default pilot");
                Assert.AreEqual($"pilot.{name.ToLowerInvariant()}.default", pilot.Id);

                var unset = new SquadSlot(vessel.Class, Element.Mass);
                Assert.AreSame(pilot, catalog.ResolvePilot(unset, vessel.Keywords), $"{name}: an unset slot must fly the default");

                catalog.GetPilotsFor(vessel, offered);
                Assert.IsNotEmpty(offered, $"{name} is offered no pilot");
                Assert.AreSame(pilot, offered[0], $"{name}: the default pilot must be listed first");
            }
        }
    }
}
#endif

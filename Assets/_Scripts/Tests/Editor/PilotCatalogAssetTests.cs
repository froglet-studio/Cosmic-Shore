#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The SHIPPED pilot assets, as Tools/Build/author_pilot_assets.py authors them: the catalog validates clean, every
    /// milestone vessel declares its keywords, and every one of them seats every callsign pilot (Garrett, 2026-10-27,
    /// Docs/Pilots/CALLSIGNS.md: one pilot per callsign, any vessel). An unset seat flies the first pilot (every vessel needs a pilot).
    /// </summary>
    [TestFixture]
    public class PilotCatalogAssetTests
    {
        const string CatalogPath = "Assets/Resources/Pilots/PilotArtifactCatalog.asset";

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
        public void EveryMilestoneVessel_SeatsEveryCallsignPilot_AndAnUnsetSeatFliesNone()
        {
            var catalog = LoadCatalog();
            var offered = new List<PilotSO>();
            var artifacts = new List<ArtifactSO>();

            foreach (var name in MilestoneVessels)
            {
                var vessel = LoadVessel(name);
                catalog.GetPilotsFor(vessel, offered);
                CollectionAssert.AreEqual(catalog.Pilots, offered, $"{name} is not offered every pilot, in authored order");
                catalog.GetArtifactsFor(vessel, artifacts);
                CollectionAssert.AreEqual(catalog.Artifacts, artifacts, $"{name} is not offered every artifact");
                Assert.AreSame(catalog.Pilots[0], catalog.ResolvePilot(new SquadSlot(vessel.Class, Element.Mass), vessel.Keywords),
                    $"{name}: an unset seat flies the first pilot");
            }
        }
    }
}
#endif

using System.IO;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Editor;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Voyages are chosen in the app shell, not in the Toy Box or the world (Garrett, 2026-10-28): the harbour is no toy
    /// and lists in no Toy Box, every line the Voyage window draws is authored, and the window's third voyage is the job
    /// cards' template.
    /// </summary>
    public class VoyageShellTests
    {
        const string ContentPath = "Assets/Resources/Voyages/VoyageContent.asset";
        const string WindowPath = "Assets/_Prefabs/UI Elements/Main Menu Screens/Mission/MissionView.prefab";

        [Test]
        public void Harbour_IsNoToy_AndNotInTheToyBox()
        {
            Assert.IsFalse(typeof(Toy).IsAssignableFrom(typeof(VoyageHarbour)),
                "VoyageHarbour is a Toy: it would stand a station to fly through in freestyle.");
            Assert.IsFalse(typeof(IToyShellSurface).IsAssignableFrom(typeof(VoyageHarbour)),
                "VoyageHarbour is an IToyShellSurface: the Toy Box would list it.");
        }

        [Test]
        public void VoyageSettings_AreNoToy_AndLoadFromResources()
        {
            Assert.IsFalse(typeof(ToyDefinitionSO).IsAssignableFrom(typeof(VoyageToyDefinitionSO)),
                "The voyage settings are a toy definition again: the toybox would stand them in freestyle.");
            var settings = AssetDatabase.LoadAssetAtPath<VoyageToyDefinitionSO>(
                "Assets/Resources/" + VoyageToyDefinitionSO.ResourcePath + ".asset");
            Assert.IsNotNull(settings, "The voyage settings are not at Resources/" + VoyageToyDefinitionSO.ResourcePath);
            Assert.IsNotNull(settings.ArkEscort, "The voyage settings name no Ark escort: the escort card would vanish.");
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(settings));
            StringAssert.DoesNotContain(guid, File.ReadAllText("Assets/Resources/Toybox.asset"),
                "The toybox lists the voyage settings: freestyle and voyages are not connected.");
        }

        [Test]
        public void VoyageScene_IsBuilt_AndCarriesNoFreestyle()
        {
            Assert.IsTrue(File.Exists(VoyageLaunch.ScenePath), VoyageLaunch.ScenePath + " is missing (FrogletTools > Scenes > Build Voyage Scene)");
            Assert.IsTrue(EditorBuildSettings.scenes.Any(sc => sc.enabled && sc.path == VoyageLaunch.ScenePath),
                "The voyage scene is not in the build list: Set sail would fail to load it.");
            CollectionAssert.Contains(WebToyboxBuild.Scenes, VoyageLaunch.ScenePath,
                "The toybox (and sim farm) build list has no voyage scene.");

            string scene = File.ReadAllText(VoyageLaunch.ScenePath);
            StringAssert.Contains("guid: " + ScriptGuid(typeof(VoyageScene)), scene, "The voyage scene has no VoyageScene director.");
            foreach (var t in new[] { typeof(ToyboxController), typeof(MenuCrystalClickHandler), typeof(MainMenuController),
                                      typeof(ModePreviewSession) })
                StringAssert.DoesNotContain("guid: " + ScriptGuid(t), scene, $"The voyage scene carries {t.Name}: freestyle baggage.");
        }

        static string ScriptGuid(System.Type t)
        {
            foreach (var g in AssetDatabase.FindAssets(t.Name + " t:MonoScript"))
            {
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));
                if (ms && ms.GetClass() == t) return g;
            }
            Assert.Fail("No script for " + t.Name);
            return null;
        }

        [Test]
        public void ShippedContent_AuthorsEveryLineTheVoyageWindowDraws()
        {
            var content = AssetDatabase.LoadAssetAtPath<VoyageContentSO>(ContentPath);
            Assert.IsNotNull(content, ContentPath + " is missing (run Tools/Build/author_voyage_assets.py)");
            Assert.IsNotEmpty(content.Lines.CardRoute);
            Assert.IsNotEmpty(content.Lines.CardHomeRoute);
            Assert.IsNotEmpty(content.Lines.CardUnderway);
            Assert.IsNotEmpty(content.Lines.NoWork);
            Assert.IsNotEmpty(content.Lines.ChooseNext);
        }

        [Test]
        public void ShippedContent_GivesTheArkEscortACourseOnTheRealSea()
        {
            // ARK_ESCORT.md §5: from each domain's harbour, an escort sails to a real port on the HyperSea and back home,
            // and the chart can measure the course (the card's light years).
            var content = AssetDatabase.LoadAssetAtPath<VoyageContentSO>(ContentPath);
            Assert.IsNotNull(content, ContentPath + " is missing (run Tools/Build/author_voyage_assets.py)");
            var c = content.Content;
            foreach (var home in new[] { CosmicShore.Data.Domains.Jade, CosmicShore.Data.Domains.Ruby, CosmicShore.Data.Domains.Gold })
            {
                var state = CosmicShore.Data.VoyageRules.NewGalaxy(c, home);
                var route = CosmicShore.Data.VoyageRules.EscortRoute(state, c);
                Assert.IsNotNull(route, $"{home}: the escort has no destination");
                Assert.AreEqual(state.Home, route[0], $"{home}: the escort sets out from home");
                Assert.AreEqual(CosmicShore.Data.VoyageExperience.Port, c.Location(route[^1]).Experience, $"{home}: it berths at a port");
                var sea = CosmicShore.Data.VoyageChart.For(c)?.Along(route);
                Assert.IsNotNull(sea, $"{home}: the chart cannot sail {string.Join(" > ", route)}");
                Assert.Greater(sea.LightYears, 0);

                Assert.IsTrue(CosmicShore.Data.VoyageRules.EscortArrived(state, c, route[^1]));
                var back = CosmicShore.Data.VoyageRules.EscortRoute(state, c);
                Assert.AreEqual(state.Home, back[^1], $"{home}: from the port, the next escort sails home");
            }
        }

        [Test]
        public void VoyageWindow_ThirdVoyage_IsTheJobCardTemplate()
        {
            string card = AssetDatabase.AssetPathToGUID("Assets/_Scripts/UI/Views/VoyageJobCard.cs");
            string jobs = AssetDatabase.AssetPathToGUID("Assets/_Scripts/UI/Views/VoyageJobsView.cs");
            Assert.IsNotEmpty(card);
            Assert.IsNotEmpty(jobs);
            string window = File.ReadAllText(WindowPath);
            StringAssert.Contains("guid: " + card, window, "The Voyage window has no job card.");
            StringAssert.Contains("guid: " + jobs, window, "The Voyage window has no jobs view to stand the cards.");
            StringAssert.Contains("m_Name: Voyage3", window);
        }
    }
}

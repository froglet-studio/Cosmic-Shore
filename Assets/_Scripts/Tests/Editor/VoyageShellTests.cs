using System.IO;
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

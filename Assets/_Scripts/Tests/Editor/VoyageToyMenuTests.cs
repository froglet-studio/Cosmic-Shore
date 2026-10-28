using System.IO;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Voyage toy reaches the menu: the Toy Box lists only <see cref="IToyShellSurface"/>s, so the toy must be one,
    /// its option's lines are authored on the shipped asset, and the Voyage window's third voyage opens it.
    /// </summary>
    public class VoyageToyMenuTests
    {
        const string ToyPath = "Assets/_SO_Assets/Toys/Toy_Voyage.asset";
        const string WindowPath = "Assets/_Prefabs/UI Elements/Main Menu Screens/Mission/MissionView.prefab";

        [Test]
        public void VoyageToy_IsAShellSurface_SoTheToyBoxListsIt()
        {
            Assert.IsTrue(typeof(IToyShellSurface).IsAssignableFrom(typeof(VoyageToy)),
                "VoyageToy is not an IToyShellSurface: it stands in the world but the Toy Box never lists it.");
        }

        [Test]
        public void ShippedToy_AuthorsEveryMenuLine()
        {
            var toy = AssetDatabase.LoadAssetAtPath<VoyageToyDefinitionSO>(ToyPath);
            Assert.IsNotNull(toy, ToyPath + " is missing (run Tools/Build/author_voyage_assets.py)");
            Assert.IsNotEmpty(toy.ShellLabel);
            Assert.IsNotEmpty(toy.ShellSetSailVerb);
            Assert.IsNotEmpty(toy.ShellUnderwayLabel);
            Assert.IsNotEmpty(toy.ShellGiveUpVerb);
        }

        [Test]
        public void VoyageWindow_ThirdVoyage_OpensTheVoyageToy()
        {
            string toyGuid = AssetDatabase.AssetPathToGUID(ToyPath);
            Assert.IsNotEmpty(toyGuid);
            string window = File.ReadAllText(WindowPath);
            StringAssert.Contains("voyageToy: {fileID: 11400000, guid: " + toyGuid, window,
                "The Voyage window has no card that opens the Voyage toy.");
            StringAssert.Contains("m_Name: Voyage3", window);
        }
    }
}

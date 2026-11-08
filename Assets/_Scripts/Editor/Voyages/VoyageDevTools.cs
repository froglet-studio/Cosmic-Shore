using CosmicShore.Core;
using CosmicShore.Editor.Froglet;
using CosmicShore.Tests;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Voyages
{
    /// <summary>
    /// Dev tools for voyages (Docs/Voyages/DEPTH.md): the depth scorecard played headless on the shipped content, and
    /// forgetting this machine's galaxy so the next voyage starts from the content's seeds. Both are READER-side (the
    /// scorecard writes nothing; forgetting the galaxy clears a PlayerPrefs record, no asset), so neither records in
    /// the tool ledger.
    /// </summary>
    public static class VoyageDevTools
    {
        [MenuItem("FrogletTools/Voyages/Depth Scorecard (headless, 40 galaxies)", false, 401)]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 3,
            Description = "Plays 40 galaxies of 60 voyages on the shipped voyage content with the pure rules and prints the depth scorecard (DEPTH.md section 11): dead boards, distinct work, news-grown share, rival events, saga reach, meetings. Takes a few seconds.")]
        public static void Scorecard()
        {
            var card = VoyageDepthHarness.Play(VoyageDepthHarness.LoadShippedContent(), 60, 40);
            Debug.Log("[Voyages] " + card.Report());
        }

        [MenuItem("FrogletTools/Voyages/Forget This Machine's Galaxy (dev)", false, 402)]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 2,
            Description = "Clears the saved voyage record on this machine: the next voyage starts a new galaxy from the content's seed news, with every rival at home and the Kaiju freshly sighted. Finds already docked home are kept (they are in the account inventory).")]
        public static void ForgetGalaxy()
        {
            VoyageStore.Clear();
            Debug.Log("[Voyages] The galaxy is forgotten: the next voyage starts from the content's seeds.");
        }
    }
}

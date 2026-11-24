#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Skim Race's garden ruins (SKIMRACE.md §5b) on the SHIPPED ribbons, as <see cref="SpawnableWaypointTrack"/> lays
    /// them (the baked copy of the scene's track, <c>SkimRaceWaypointTrack.prefab</c>): I1-I3 lay none, and Relativity
    /// (I4) carries seven that keep every rule <see cref="SkimRaceRuinsTests.AssertRules"/> holds, within a prism
    /// budget. A change to the track that takes a ruin away, or pushes one into the lane, fails here.
    /// </summary>
    public class SkimRaceRuinsShippedTrackTests
    {
        const string BakePath = "Assets/_Prefabs/Environment/Spawners/SkimRaceWaypointTrack.prefab";

        GameObject _root;
        SpawnableWaypointTrack _track;

        [SetUp]
        public void SetUp()
        {
            _root = PrefabUtility.LoadPrefabContents(BakePath);
            _track = _root.GetComponentInChildren<SpawnableWaypointTrack>();
            Assert.IsNotNull(_track, $"{BakePath} carries no SpawnableWaypointTrack");
        }

        [TearDown]
        public void TearDown() => PrefabUtility.UnloadPrefabContents(_root);

        SkimRaceRuinsTests.Laid Lay(int intensity) => SkimRaceRuinsTests.Lay(intensity, Vector3.zero, laid =>
        {
            foreach (var block in _track.GetPreviewBlocks(intensity))
            {
                laid.Ribbon.Add(block.Position);
                laid.Marker.Add(block.IsMarker);
            }
        });

        [Test]
        public void LegacyIntensities_LayNoRuins()
        {
            for (int intensity = 1; intensity <= 3; intensity++)
                Assert.AreEqual(0, Lay(intensity).Parts.Count, $"I{intensity} lays no ruin");
        }

        [Test]
        public void Relativity_SevenRuins_KeepTheRules_WithinBudget()
        {
            var laid = Lay(4);
            Assert.AreEqual(_track.CrystalsPerLap(4), laid.Gates.Count, "one gate for each crystal marker");
            Assert.AreEqual(7, laid.RuinLegs().Count, "seven ruins a lap");
            SkimRaceRuinsTests.AssertRules(laid);

            int prisms = 0;
            foreach (bool keep in laid.Keep) prisms += keep ? 1 : 0;
            Assert.GreaterOrEqual(prisms, 7 * SkimRaceRuins.MinParts, "the ruins are a few loose stones");
            Assert.LessOrEqual(prisms, 800, "the ruins cost more prisms than the ribbon itself (1,015)");
        }
    }
}
#endif

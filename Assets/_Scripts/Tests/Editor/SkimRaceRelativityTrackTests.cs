#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Skim Race intensity 4 ("Relativity", SKIMRACE.md §5a) as the C# lays it - a cross-check of
    /// <c>Tools/Build/author_skimrace_relativity_track.py</c>, which validates the same layout on its
    /// own Python port of <see cref="SpawnableWaypointTrack"/>. Reads the baked copy of the scene's
    /// track component (<c>SkimRaceWaypointTrack.prefab</c>; the generator writes both and refuses to
    /// write if they differ).
    /// </summary>
    public class SkimRaceRelativityTrackTests
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

        [Test]
        public void CrystalsPerLap_LegacyIntensitiesFallBackToWaypointCount_RelativityAuthors24()
        {
            for (int intensity = 1; intensity <= 3; intensity++)
                Assert.AreEqual(_track.waypoints[intensity - 1].positions.Count, _track.CrystalsPerLap(intensity),
                    $"I{intensity} authors no crystalsPerLap, so a lap must stay worth one crystal per waypoint");

            Assert.AreEqual(24, _track.CrystalsPerLap(4));
            Assert.Greater(_track.waypoints[3].positions.Count, 24,
                "I4's waypoints are a dense spline sample - if this fails the target would be waypoints x laps");
        }

        [Test]
        public void LegacyIntensity_KeepsWorldUp()
        {
            foreach (var block in _track.GetPreviewBlocks(1))
                Assert.Greater(Vector3.Dot(block.Rotation * Vector3.up, Vector3.up), 0.999f,
                    "the flat octagon authors no ribbon normals and must lay with world up as before");
        }

        [Test]
        public void Relativity_RibbonNormalsArePerpendicularAndContinuous()
        {
            var blocks = _track.GetPreviewBlocks(4).ToList();
            Assert.Greater(blocks.Count, 800);

            // The start: lobe 0's apex, heading +Z, ribbon horizontal (the grid sits above and below it).
            Assert.Greater(Vector3.Dot(blocks[0].Rotation * Vector3.forward, Vector3.forward), 0.97f);
            Assert.Greater(Vector3.Dot(blocks[0].Rotation * Vector3.up, Vector3.up), 0.97f);

            float worstStep = 0f;
            for (int i = 0; i < blocks.Count; i++)
            {
                Vector3 up = blocks[i].Rotation * Vector3.up;
                Vector3 nextUp = blocks[(i + 1) % blocks.Count].Rotation * Vector3.up;
                worstStep = Mathf.Max(worstStep, Vector3.Angle(up, nextUp));
            }
            // 12 u apart, a quarter roll over a 600 u pass plus the lobe's own curvature: a few degrees.
            // A sign slip between two waypoint normals would show here as a ~180-degree step.
            Assert.Less(worstStep, 10f, "the ribbon flips between two consecutive prisms");
        }

        [Test]
        public void Relativity_StrandsNeverComeWithin60Units()
        {
            var p = _track.GetPreviewBlocks(4).Select(b => b.Position).ToList();
            int n = p.Count;
            const int gap = 40;   // 480 u along the track: anything nearer is the same strand
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            for (int j = i + gap; j < n; j++)
            {
                if (n - (j - i) < gap) continue;
                best = Mathf.Min(best, Vector3.Distance(p[i], p[j]));
            }
            Assert.GreaterOrEqual(best, 60f, "two strands of the knot pass closer than the generator allows");
        }

        [Test]
        public void Relativity_ThreadsTheNucleusSixTimesALap()
        {
            var p = _track.GetPreviewBlocks(4).Select(b => b.Position).ToList();
            int entries = 0;
            for (int i = 0; i < p.Count; i++)
                if (p[i].magnitude < 120f && p[(i + p.Count - 1) % p.Count].magnitude >= 120f) entries++;
            Assert.AreEqual(6, entries);
        }
    }
}
#endif

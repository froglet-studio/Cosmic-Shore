#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pure-geometry tests for the fly-by-numbers painting presets and the ShapeDefinition
    /// converter. These guard the invariants the <see cref="PaintingRunner"/> relies on:
    /// every stroke is flyable (≥2 points), every domain is a real team colour, paintings sit
    /// on their base plane (y ≥ 0), and the Taj Mahal actually is the monumental, three-domain,
    /// many-stroke painting the toy advertises.
    /// </summary>
    public class PaintingPresetLibraryTests
    {
        static readonly Domains[] PaintableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        static void AssertStrokesWellFormed(List<PaintingStroke> strokes)
        {
            Assert.IsNotNull(strokes);
            Assert.Greater(strokes.Count, 0, "preset generated no strokes");
            foreach (var s in strokes)
            {
                Assert.IsNotNull(s.points);
                Assert.GreaterOrEqual(s.points.Count, 2, $"stroke '{s.name}' is not flyable");
                Assert.IsFalse(string.IsNullOrEmpty(s.name), "stroke has no name");
                CollectionAssert.Contains(PaintableDomains, s.domain,
                    $"stroke '{s.name}' uses a non-paintable domain");
            }
        }

        [TestCase(PaintingPreset.Star)]
        [TestCase(PaintingPreset.Rainbow)]
        [TestCase(PaintingPreset.Saturn)]
        [TestCase(PaintingPreset.TajMahal)]
        public void EveryPreset_GeneratesWellFormedStrokes(PaintingPreset preset)
        {
            var strokes = PaintingPresetLibrary.Generate(preset, 1000f);
            AssertStrokesWellFormed(strokes);

            // Paintings are authored with their base plane at y=0 — nothing dips below it
            // (some, like Saturn, deliberately float above it).
            float minY = strokes.SelectMany(s => s.points).Min(p => p.y);
            Assert.GreaterOrEqual(minY, -1f, "painting dips below its base plane");
        }

        [Test]
        public void Star_IsOneBigSingleDomainStroke()
        {
            var strokes = PaintingPresetLibrary.Generate(PaintingPreset.Star, 420f);
            Assert.AreEqual(1, strokes.Count);

            var bounds = PaintingPresetLibrary.ComputeBounds(strokes);
            Assert.Greater(bounds.size.y, 300f, "the low-end shape should not be little");
        }

        [Test]
        public void Rainbow_UsesAllThreeDomains_OnePerBand()
        {
            var strokes = PaintingPresetLibrary.Generate(PaintingPreset.Rainbow, 700f);
            Assert.AreEqual(3, strokes.Count);
            CollectionAssert.AreEquivalent(PaintableDomains, strokes.Select(s => s.domain).ToArray());
        }

        [Test]
        public void Saturn_RingsAreGenuinelyThreeDimensional()
        {
            var strokes = PaintingPresetLibrary.Generate(PaintingPreset.Saturn, 800f);
            Assert.AreEqual(3, strokes.Count);

            var ring = strokes.First(s => s.name.Contains("Outer"));
            float zSpan = ring.points.Max(p => p.z) - ring.points.Min(p => p.z);
            Assert.Greater(zSpan, 100f, "the ring should tilt out of the picture plane");
        }

        [Test]
        public void TajMahal_IsTheMonument()
        {
            const float W = 1100f;
            var strokes = PaintingPresetLibrary.Generate(PaintingPreset.TajMahal, W);

            Assert.GreaterOrEqual(strokes.Count, 50, "the Taj should take many strokes");
            CollectionAssert.AreEquivalent(PaintableDomains,
                strokes.Select(s => s.domain).Distinct().ToArray(),
                "the Taj should use all three domains");

            var bounds = PaintingPresetLibrary.ComputeBounds(strokes);
            Assert.Greater(bounds.size.y, 0.6f * W, "the monument should tower");
            Assert.Less(bounds.size.y, 0.85f * W);
            Assert.Greater(bounds.size.x, 0.9f * W, "the plinth should span the full width");

            float pathLength = PaintingPresetLibrary.TotalPathLength(strokes);
            Assert.Greater(pathLength, 15f * W, "the Taj should be hours of flying, not minutes");

            // Domain switches happen at meaningful boundaries, not every stroke.
            int switches = 0;
            for (int i = 1; i < strokes.Count; i++)
                if (strokes[i].domain != strokes[i - 1].domain) switches++;
            Assert.GreaterOrEqual(switches, 4, "the Taj should exercise the domain gates");
            Assert.Less(switches, strokes.Count / 2, "colour batching keeps switches meaningful");

            // Four of everything four-fold: minarets, chhatris, corner towers.
            Assert.AreEqual(4, strokes.Count(s => s.name.EndsWith("Minaret")));
            Assert.AreEqual(4, strokes.Count(s => s.name.Contains("Chhatri Canopy")));
            Assert.AreEqual(4, strokes.Count(s => s.name.Contains("Corner Tower")));
        }

        [Test]
        public void TajMahal_SegmentsStayFlyable()
        {
            var strokes = PaintingPresetLibrary.Generate(PaintingPreset.TajMahal, 1100f);
            foreach (var s in strokes)
            {
                for (int i = 1; i < s.points.Count; i++)
                {
                    float seg = Vector3.Distance(s.points[i - 1], s.points[i]);
                    Assert.Greater(seg, 1f, $"'{s.name}' has a degenerate segment");
                    Assert.Less(seg, 1200f, $"'{s.name}' has an implausibly long segment");
                }
            }
        }

        [Test]
        public void FromShape_SplitsPenUpGapsIntoStrokes()
        {
            var smiley = ScriptableObject.CreateInstance<ShapeDefinition>();
            try
            {
                smiley.shapeName = "Smiley";
                smiley.GeneratePreset(ShapePreset.Smiley, 100f);

                var strokes = PaintingPresetLibrary.FromShape(smiley, Domains.Gold, 2.5f);
                AssertStrokesWellFormed(strokes);
                Assert.Greater(strokes.Count, 1, "the smiley's pen-up gaps should split it into strokes");
                Assert.IsTrue(strokes.All(s => s.domain == Domains.Gold));

                // The converter re-bases shapes so the lowest point sits exactly on y=0.
                float minY = strokes.SelectMany(s => s.points).Min(p => p.y);
                Assert.AreEqual(0f, minY, 0.01f, "converted shapes should sit on the base plane");
            }
            finally
            {
                Object.DestroyImmediate(smiley);
            }
        }

        [Test]
        public void PaintingDefinition_ResolvesPresetWithoutMutatingAsset()
        {
            var def = ScriptableObject.CreateInstance<PaintingDefinitionSO>();
            try
            {
                def.SetRuntimeData("test_taj", "Taj Mahal", "", PaintingPreset.TajMahal, 1100f, 26f);
                def.EnsureStrokes();
                Assert.GreaterOrEqual(def.Strokes.Count, 50);
                Assert.Greater(def.LocalBounds.size.y, 600f);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void ComputeBounds_EmptyStrokes_IsZero()
        {
            Assert.AreEqual(Vector3.zero, PaintingPresetLibrary.ComputeBounds(new List<PaintingStroke>()).size);
            Assert.AreEqual(Vector3.zero, PaintingPresetLibrary.ComputeBounds(null).size);
        }

        [Test]
        public void ShareExporter_BuildsSelfContainedViewer()
        {
            var records = new List<PaintingPrismRecord>
            {
                PaintingPrismRecord.From(new Vector3(1f, 2f, 3f), Quaternion.identity,
                    new Vector3(4f, 5f, 6f), Domains.Gold, PrismType.Squirrel),
                PaintingPrismRecord.From(Vector3.zero, Quaternion.Euler(0f, 45f, 0f),
                    Vector3.one, Domains.Jade, PrismType.Interactive),
            };

            string html = PaintingShareExporter.BuildHtml("Taj & Test <Monument>", records,
                new Color(0f, 1f, 0.5f), new Color(1f, 0f, 0.3f), new Color(1f, 0.8f, 0f));

            StringAssert.Contains("<canvas", html, "viewer needs a canvas");
            StringAssert.Contains("webgl", html, "viewer renders with WebGL");
            StringAssert.Contains("Taj &amp; Test &lt;Monument&gt;", html, "title must be HTML-escaped");
            Assert.IsFalse(html.Contains("__DATA__"), "data token must be substituted");
            Assert.IsFalse(html.Contains("__TITLE__"), "title token must be substituted");
            Assert.IsFalse(html.Contains("__PALETTE__"), "palette token must be substituted");
            Assert.IsFalse(html.Contains("src="), "viewer must not reference external scripts");
        }

        [Test]
        public void ShareExporter_FallbackFromStrokes_CoversEverySegment()
        {
            var def = ScriptableObject.CreateInstance<PaintingDefinitionSO>();
            try
            {
                def.SetRuntimeData("test_rainbow", "Rainbow", "", PaintingPreset.Rainbow, 700f, 30f);
                def.EnsureStrokes();

                int expectedSegments = 0;
                foreach (var s in def.Strokes) expectedSegments += s.points.Count - 1;

                var records = PaintingShareExporter.FallbackFromStrokes(def);
                Assert.AreEqual(expectedSegments, records.Count);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }
    }
}
#endif

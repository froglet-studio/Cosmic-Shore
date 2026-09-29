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
    /// Pure-geometry tests for the connect-the-dots painting presets and the ShapeDefinition
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

            // Paintings are authored with their base plane at y=0 - nothing dips below it
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

        // ── Grandiose 3D constructions ───────────────────────────────────────

        static readonly PaintingPreset[] GrandiosePresets =
        {
            PaintingPreset.Nautilus, PaintingPreset.Lotus, PaintingPreset.Rose, PaintingPreset.Buckyball,
            PaintingPreset.TorusKnot, PaintingPreset.DoubleHelix, PaintingPreset.SpiralGalaxy,
            PaintingPreset.LionsHead, PaintingPreset.Phoenix, PaintingPreset.Peacock,
            PaintingPreset.StarryNight, PaintingPreset.BobRossVista,
        };

        [Test]
        public void GrandiosePreset_IsWellFormedFlyableAndNonPlanar(
            [ValueSource(nameof(GrandiosePresets))] PaintingPreset preset)
        {
            const float W = 1200f;
            var strokes = PaintingPresetLibrary.Generate(preset, W);
            AssertStrokesWellFormed(strokes); // ≥2 pts, named, ONLY Jade/Ruby/Gold

            // Base plane at y=0 - every generator rebases.
            float minY = strokes.SelectMany(s => s.points).Min(p => p.y);
            Assert.GreaterOrEqual(minY, -1f, $"{preset} dips below its base plane");

            // Genuinely non-planar: real extent on all three axes (not a billboard).
            var b = PaintingPresetLibrary.ComputeBounds(strokes);
            Assert.Greater(b.size.x, 0.05f * W, $"{preset} has no x-extent");
            Assert.Greater(b.size.y, 0.05f * W, $"{preset} has no y-extent");
            Assert.Greater(b.size.z, 0.05f * W, $"{preset} is planar - no z-extent");

            // Grandiose: eclipses or matches the Taj Mahal in flight. Reference-grade rebuilds keep
            // HONEST proportions, so minimums are per-preset - a trefoil tube is 19 elegant strokes,
            // not 40 padded ones, and a true-scale DNA molecule flies ~14·W.
            var (minStrokes, minPathW) = preset switch
            {
                PaintingPreset.TorusKnot => (18, 30f),
                PaintingPreset.DoubleHelix => (60, 12f),
                PaintingPreset.Rose => (50, 15f),
                PaintingPreset.SpiralGalaxy => (100, 15f),
                _ => (40, 20f),
            };
            Assert.GreaterOrEqual(strokes.Count, minStrokes, $"{preset} is not grandiose enough");
            Assert.Greater(PaintingPresetLibrary.TotalPathLength(strokes), minPathW * W, $"{preset} is too short");

            // Every segment flyable: no NaN, no degenerate (<0.4u) or unflyable (>0.65·W) jump.
            foreach (var s in strokes)
                for (int i = 1; i < s.points.Count; i++)
                {
                    Vector3 p = s.points[i];
                    Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z), $"{preset} NaN point");
                    float seg = Vector3.Distance(s.points[i - 1], p);
                    Assert.Greater(seg, 0.4f, $"{preset} '{s.name}' has a degenerate segment");
                    Assert.Less(seg, 0.65f * W, $"{preset} '{s.name}' has an unflyable jump");
                }
        }

        [Test]
        public void GrandiosePreset_IsDeterministic(
            [ValueSource(nameof(GrandiosePresets))] PaintingPreset preset)
        {
            var a = PaintingPresetLibrary.Generate(preset, 1000f);
            var c = PaintingPresetLibrary.Generate(preset, 1000f);
            Assert.AreEqual(a.Count, c.Count, $"{preset} stroke count not stable");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].domain, c[i].domain);
                Assert.AreEqual(a[i].points.Count, c[i].points.Count);
                for (int p = 0; p < a[i].points.Count; p++)
                    Assert.AreEqual(a[i].points[p], c[i].points[p], $"{preset} stroke {i} point {p} not stable");
            }
        }

        [Test]
        public void LionsHead_HasHundredsOfManeStrokes()
        {
            var strokes = PaintingPresetLibrary.Generate(PaintingPreset.LionsHead, 1100f);
            Assert.GreaterOrEqual(strokes.Count(s => s.name.StartsWith("Mane")), 120,
                "the lion's mane should be built from many curl-field strands");
        }

        [Test]
        public void Buckyball_HasTwelvePentagonsTwentyHexagonsThirtyDoubleBonds()
        {
            var strokes = PaintingPresetLibrary.Generate(PaintingPreset.Buckyball, 1000f);
            Assert.AreEqual(12, strokes.Count(s => s.name.StartsWith("Pentagon")), "a soccer ball has 12 pentagons");
            Assert.AreEqual(20, strokes.Count(s => s.name.StartsWith("Hexagon")), "a soccer ball has 20 hexagons");
            Assert.AreEqual(30, strokes.Count(s => s.name.StartsWith("Double Bond")),
                "C60 has exactly 30 hexagon-hexagon (6:6) double bonds");
        }

        [Test]
        public void ReferenceRebuilds_KeepTheirAnatomy()
        {
            // The reference-grade forms carry their real structural counts - locked so a future tweak
            // can't silently drop the anatomy that makes them read as real.
            var nautilus = PaintingPresetLibrary.Generate(PaintingPreset.Nautilus, 900f);
            Assert.AreEqual(58, nautilus.Count(s => s.name.StartsWith("Growth Line")),
                "the nautilus reads real because of its growth-line ribs");

            var dna = PaintingPresetLibrary.Generate(PaintingPreset.DoubleHelix, 900f);
            Assert.AreEqual(4, dna.Count(s => s.name.StartsWith("Backbone")), "two strands, ribboned = 4 helices");
            Assert.AreEqual(28, dna.Count(s => s.name.EndsWith("Purine")), "10 bp/turn × 2.8 turns");

            var galaxy = PaintingPresetLibrary.Generate(PaintingPreset.SpiralGalaxy, 1200f);
            Assert.AreEqual(2, galaxy.Count(s => s.name.EndsWith("Dust Lane")), "grand designs have TWO arms");

            // The lotus is petals all the way down: wide-open outer whorls closing to the bud
            // core (10+9+8+6+5); each petal is an outline + a midrib stroke.
            var lotus = PaintingPresetLibrary.Generate(PaintingPreset.Lotus, 900f);
            Assert.AreEqual(38, lotus.Count(s => s.name.Contains("Petal")), "10+9+8+6+5 petals, nothing else");
            Assert.AreEqual(lotus.Count(s => s.name.Contains("Petal")), lotus.Count(s => s.name.Contains("Rib")));

            // The enchanted rose: long stem + two leaflets + sepals under a compact wrapped bloom.
            var rose = PaintingPresetLibrary.Generate(PaintingPreset.Rose, 900f);
            Assert.AreEqual(27, rose.Count(s => s.name.Contains("Petal")), "8+8+6+5 wrapping petals");
            Assert.AreEqual(1, rose.Count(s => s.name == "Stem"));
            Assert.AreEqual(2, rose.Count(s => s.name.StartsWith("Leaf ") && !s.name.Contains("Vein")));
            Assert.AreEqual(5, rose.Count(s => s.name.StartsWith("Sepal")));
            Assert.AreEqual(1, rose.Count(s => s.name.StartsWith("Furled Heart")));
            // the stem owns the composition: the bloom sits in the top ~40% of the height
            var bounds = PaintingPresetLibrary.ComputeBounds(rose);
            Assert.Greater(bounds.size.y, 0.7f * 900f, "the enchanted rose is tall");
            Assert.Greater(bounds.size.y, 1.5f * Mathf.Max(bounds.size.x, bounds.size.z),
                "stem-dominant proportions - much taller than wide");
        }
    }
}
#endif

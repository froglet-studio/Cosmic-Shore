using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The 2026-08-15 incident class, applied to every generator: across a wide genome sweep
    /// (random chimeras, humans, and every axis pinned to both extremes) no emitted vertex is
    /// NaN or infinite, no part is empty, and every seam feature passes the attachment contract
    /// (the assembler throws otherwise, so a green run IS the assertion).
    /// </summary>
    public class CharacterNaNSweepTests
    {
        static void AssertClean(CharacterModel model, string label)
        {
            foreach (var part in model.AllParts())
            {
                Assert.Greater(part.Verts.Count, 0, $"{label}: {part.Name} is empty");
                Assert.AreEqual(0, part.Tris.Count % 3, $"{label}: {part.Name} triangle list");
                Assert.IsFalse(GeometryKit.AnyNaN(part), $"{label}: NaN/Inf in {part.Name}");
                foreach (var n in part.Normals)
                    Assert.IsFalse(float.IsNaN(n.x) || float.IsNaN(n.y) || float.IsNaN(n.z), $"{label}: NaN normal in {part.Name}");
                foreach (var t in part.Tris) Assert.That(t, Is.InRange(0, part.Verts.Count - 1), $"{label}: {part.Name} index out of range");
            }
            var b = model.ComputeBounds();
            Assert.Less(b.size.magnitude, 6f, $"{label}: bounds exploded {b.size}");
        }

        [Test]
        public void RandomChimerasAndHumansEmitNoNaN()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            for (int seed = 100; seed < 160; seed++)
            {
                var g = GenomeRoller.RollChimera(seed, catalog, config);
                AssertClean(CharacterTestKit.Build(g, catalog, config, HeadDetail.Runtime), g.Label());
            }
            for (int seed = 0; seed < 15; seed++)
            {
                var g = GenomeRoller.RollHuman(seed, config);
                AssertClean(CharacterTestKit.Build(g, catalog, config, HeadDetail.Runtime), g.Label());
            }
        }

        [Test]
        public void EveryCladePairAtEveryWeightCornerEmitsNoNaN()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            var animals = catalog.Animals();
            var corners = new[]
            {
                new CharacterWeights { Human = 0.6f, CladeA = 0.2f, CladeB = 0.2f },
                new CharacterWeights { Human = 0.2f, CladeA = 0.6f, CladeB = 0.2f },
                new CharacterWeights { Human = 0.2f, CladeA = 0.2f, CladeB = 0.6f },
            };
            foreach (var a in animals)
                foreach (var b in animals)
                {
                    if (a == b) continue;
                    foreach (var w in corners)
                    {
                        var g = GenomeRoller.RollChimera(1, catalog, config);
                        g.CladeA = a.Key; g.CladeB = b.Key; g.Weights = w;
                        AssertClean(CharacterTestKit.Build(g, catalog, config, HeadDetail.Runtime), g.Label());
                    }
                }
        }

        [Test]
        public void ExtremeHumanBaseShapesEmitNoNaN()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            foreach (var extreme in new[] { -1f, 1f })
            {
                var g = GenomeRoller.RollHuman(2, config);
                var s = HeadShape.Neutral;
                for (int i = 0; i < HeadShape.AxisCount; i++) s[(HeadAxis)i] = extreme;
                g.BaseShape = s;
                AssertClean(CharacterTestKit.Build(g, catalog, config, HeadDetail.Runtime), $"human all axes {extreme}");
            }
        }

        [Test]
        public void PaintedTexturesAreFinite()
        {
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            var g = GenomeRoller.RollChimera(555, catalog, config);
            var model = CharacterTestKit.Build(g, catalog, config, HeadDetail.Runtime);
            var t = CharacterTexturePainter.Paint(model, config, null, 96);
            foreach (var c in new List<TextureCanvas> { t.Skin, t.SkinNormal, t.Eye, t.Keratin, t.Hair })
                for (int i = 0; i < c.R.Length; i++)
                {
                    Assert.IsFalse(float.IsNaN(c.R[i]) || float.IsNaN(c.G[i]) || float.IsNaN(c.B[i]) || float.IsNaN(c.A[i]));
                    Assert.That(c.R[i], Is.InRange(0f, 1f));
                }
        }
    }
}

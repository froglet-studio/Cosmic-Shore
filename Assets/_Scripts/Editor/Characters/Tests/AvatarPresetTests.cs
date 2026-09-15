using System.Collections.Generic;
using System.IO;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The shipped avatar recreations are ordinary genomes: every preset parses, names clades
    /// that exist, keeps its weights inside the region, assembles clean, and the fields the
    /// avatar pass added (gear, coat tint) reach the portrait-cache key.
    /// </summary>
    public class AvatarPresetTests
    {
        static List<AvatarPresets.Preset> LoadPresets()
        {
            var list = AvatarPresets.FromResources();
            if (list.Count > 0) return list;
            // Outside the editor's Resources pipeline (the offline harness) read the folder directly.
            var dir = Path.Combine("Assets", "_Scripts", "Controller", "Characters", "Resources", "Characters", "AvatarPresets");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.json"))
                    if (CharacterGenome.TryFromJson(File.ReadAllText(f), out var g))
                        list.Add(new AvatarPresets.Preset { Name = Path.GetFileNameWithoutExtension(f), IconName = AvatarPresets.IconNameOf(Path.GetFileNameWithoutExtension(f)), Genome = g });
            return list;
        }

        [Test]
        public void EveryPresetParsesNamesRealCladesAndKeepsItsWeights()
        {
            var presets = LoadPresets();
            var catalog = CharacterTestKit.Catalog();
            Assert.GreaterOrEqual(presets.Count, 18, "one preset per shipped profile icon");
            foreach (var p in presets)
            {
                Assert.IsFalse(string.IsNullOrEmpty(p.IconName), $"{p.Name}: icon name");
                Assert.IsNotNull(catalog.Require(p.Genome.CladeA), $"{p.Name}: clade A");
                Assert.IsNotNull(catalog.Require(p.Genome.CladeB), $"{p.Name}: clade B");
                Assert.IsTrue(p.Genome.Weights.IsValid, $"{p.Name}: weights {p.Genome.Weights.Human}/{p.Genome.Weights.CladeA}/{p.Genome.Weights.CladeB}");
                Assert.Greater(p.Genome.CoatTint.a, 0f, $"{p.Name}: a preset authors its coat");
            }
        }

        [Test]
        public void EveryPresetAssemblesClean()
        {
            var presets = LoadPresets();
            var catalog = CharacterTestKit.Catalog();
            var config = CharacterTestKit.Config();
            foreach (var p in presets)
            {
                var model = CharacterTestKit.Build(p.Genome, catalog, config, HeadDetail.Runtime);
                foreach (var part in model.AllParts())
                {
                    Assert.IsFalse(GeometryKit.AnyNaN(part), $"{p.Name}: {part.Name}");
                    Assert.Greater(part.Verts.Count, 0, $"{p.Name}: {part.Name} is empty");
                }
                bool hasTorso = false;
                foreach (var part in model.Parts) if (part.Name == "Torso") hasTorso = true;
                Assert.IsTrue(hasTorso, $"{p.Name}: every bust wears the jacket");
                if (p.Genome.Gear != GearKind.None)
                    Assert.IsTrue(model.Parts.Exists(x => x.Name.Contains("Goggles")), $"{p.Name}: gear {p.Genome.Gear} placed");
            }
        }

        [Test]
        public void GearAndCoatReachTheCacheKey()
        {
            var g = CharacterGenome.Empty;
            g.Seed = 5; g.CladeA = "Felidae"; g.CladeB = "Corvidae"; g.Weights = CharacterWeights.EqualThirds;
            string a = g.ContentHash();
            g.Gear = GearKind.GogglesUp;
            string b = g.ContentHash();
            g.CoatTint = new Color(1f, 0.5f, 0.5f, 1f);
            string c = g.ContentHash();
            Assert.AreNotEqual(a, b, "gear changes the portrait");
            Assert.AreNotEqual(b, c, "coat tint changes the portrait");
        }

        [Test]
        public void StylizerIsDeterministicFramedAndFinite()
        {
            const int w = 48, h = 48;
            var rgba = new float[w * h * 4];
            var rng = new CharacterRandom(3);
            for (int i = 0; i < w * h; i++)
            {
                int x = i % w, y = i / w;
                bool inBust = (x - 24) * (x - 24) + (y - 24) * (y - 24) < 14 * 14;
                rgba[i * 4] = rng.NextFloat(); rgba[i * 4 + 1] = rng.NextFloat(); rgba[i * 4 + 2] = rng.NextFloat();
                rgba[i * 4 + 3] = inBust ? 1f : 0f;
            }
            var style = PortraitStyle.Default;
            var a = PortraitStylizer.Apply(rgba, w, h, style, new Color(0f, 1f, 1f, 1f), 9);
            var b = PortraitStylizer.Apply(rgba, w, h, style, new Color(0f, 1f, 1f, 1f), 9);
            Assert.AreEqual(a.Length, rgba.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.IsFalse(float.IsNaN(a[i]) || float.IsInfinity(a[i]), "finite");
                Assert.AreEqual(a[i], b[i], "deterministic");
            }
            Assert.AreEqual(0f, a[3], "a corner is outside the octagon");
            Assert.AreEqual(1f, a[(24 * w + 24) * 4 + 3], "the centre is inside the frame");
        }
    }
}

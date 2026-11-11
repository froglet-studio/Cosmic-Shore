using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Shatterplane (GameModes.Shatterplane = 90), the Brittle Star's Space game: the plates each shipped prefab lays
    /// (<see cref="ShatterplanePlates"/> read with the prefab's own fields), the open field and the spawn clear zone,
    /// the crystal sites on the plates' axes, the ring's square-versus-tilted yield through the Brittle Star's OWN disk
    /// rule (<see cref="BrittleStarRules.DiskReaches"/>), the AI's line-up, the scoring and the per-intensity target. The
    /// census rows are the numbers <c>Tools/Build/shatterplane_budget.py</c> prints; the generator fails if they and these
    /// drift apart.
    /// </summary>
    public class ShatterplaneTests
    {
        const string PrefabDir = "Assets/_Prefabs/Spawnables";
        /// <summary>The scene's spawn ring and the clear zone the arena keeps inside it (author_shatterplane_assets.py).</summary>
        const float SpawnRing = 780f;
        const float SpawnClear = 250f;

        /// <summary>The fields of the SHIPPED prefab for an intensity, read from its YAML.</summary>
        static ShatterplanePlates.Settings ReadRung(int intensity, out int crystalCount)
        {
            string path = Path.Combine(PrefabDir, $"SpawnableShatterplane{intensity}.prefab");
            Assert.IsTrue(File.Exists(path), $"{path} is missing - run Tools/Build/author_shatterplane_assets.py");
            string yaml = File.ReadAllText(path);
            float F(string key)
            {
                var m = Regex.Match(yaml, $@"^  {key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, $"{path} has no {key}");
                return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            crystalCount = (int)F("crystalCount");
            return new ShatterplanePlates.Settings
            {
                Seed = (int)F("seed"),
                InnerRadius = F("innerRadius"),
                OuterRadius = F("outerRadius"),
                StructureCount = (int)F("structureCount"),
                PlateRadius = (int)F("plateRadius"),
                PlatesPerStack = (int)F("platesPerStack"),
                ShelfGap = F("shelfGap"),
                FaceOut = F("faceOut") != 0f,
                RibEvery = (int)F("ribEvery"),
                DangerRim = F("dangerRim") != 0f,
                ScreenEvery = (int)F("screenEvery"),
                ScreenRadius = (int)F("screenRadius"),
                ScreenOffset = F("screenOffset"),
                ScreenRibEvery = (int)F("screenRibEvery"),
            };
        }

        static List<ShatterplanePlates.TileLay> Lay(ShatterplanePlates.Settings s,
                                                    out List<ShatterplanePlates.Structure> structures)
        {
            structures = new List<ShatterplanePlates.Structure>();
            ShatterplanePlates.Layout(s, structures);
            var tiles = new List<ShatterplanePlates.TileLay>();
            ShatterplanePlates.Tiles(structures, s, tiles);
            return tiles;
        }

        // ── The arena ──

        [TestCase(1, 5290, 0, 0)]
        [TestCase(2, 5292, 0, 0)]
        [TestCase(3, 6615, 0, 0)]
        [TestCase(4, 4588, 704, 1356)]
        public void Census_MatchesTheBudgetMirror(int intensity, int total, int danger, int super)
        {
            var tiles = Lay(ReadRung(intensity, out _), out _);
            int d = 0, s = 0;
            foreach (var t in tiles)
            {
                if (t.Kind == PrismKind.Danger) d++;
                else if (t.Kind == PrismKind.SuperShielded) s++;
            }
            Assert.AreEqual(total, tiles.Count, "tile count drifted from shatterplane_budget.py");
            Assert.AreEqual(danger, d, "danger count drifted from shatterplane_budget.py");
            Assert.AreEqual(super, s, "rib and screen count drifted from shatterplane_budget.py");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Field_IsOpen_AndClearOfTheSpawnRing(int intensity)
        {
            var s = ReadRung(intensity, out _);
            var structures = new List<ShatterplanePlates.Structure>();
            int placed = ShatterplanePlates.Layout(s, structures);
            Assert.AreEqual(s.StructureCount, placed, "a structure found no room in the field");
            for (int i = 0; i < structures.Count; i++)
            {
                var a = structures[i];
                Assert.LessOrEqual(a.Centre.magnitude + a.Radius, SpawnRing - SpawnClear,
                    $"I{intensity}: structure {a.Index} reaches into the spawn ring's clear zone");
                Assert.GreaterOrEqual(Vector3.Dot(a.N, a.Centre), 0f, $"I{intensity}: structure {a.Index} faces inward");
                for (int j = i + 1; j < structures.Count; j++)
                {
                    var b = structures[j];
                    float clear = Vector3.Distance(a.Centre, b.Centre) - a.Radius - b.Radius;
                    Assert.GreaterOrEqual(clear, ShatterplanePlates.Gap - 1e-2f,
                        $"I{intensity}: structures {a.Index} and {b.Index} stand {clear:F1} u apart, under the gap");
                }
            }
        }

        [Test]
        public void Ladder_AsksOneQuestionPerRung()
        {
            var i1 = ReadRung(1, out _);
            Assert.IsTrue(i1.FaceOut, "I1's plates face straight out at the spawn ring");
            var structures = new List<ShatterplanePlates.Structure>();
            ShatterplanePlates.Layout(i1, structures);
            foreach (var st in structures)
                Assert.Greater(Vector3.Dot(st.N, st.Centre.normalized), 0.9999f, "an I1 plate is not square to the centre line");
            Assert.IsFalse(ReadRung(2, out _).FaceOut, "I2's plates are at mixed angles");
            Assert.Greater(ReadRung(3, out _).PlatesPerStack, 1, "I3 stacks its plates");
            Assert.Greater(ReadRung(3, out _).ShelfGap, 50f, "I3's shelves stand far enough apart to read as separate plates");
            var i4 = ReadRung(4, out _);
            Assert.IsTrue(i4.RibEvery > 0 && i4.DangerRim && i4.ScreenEvery > 0, "I4 has ribs, danger rims and screens");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void CrystalSites_FloatOnAPlatesAxis_InFrontOfIt(int intensity)
        {
            var s = ReadRung(intensity, out int count);
            Lay(s, out var structures);
            var sites = new List<Vector3>();
            ShatterplanePlates.CrystalSites(structures, s, count, sites);
            Assert.AreEqual(count, sites.Count);
            float front = ShatterplanePlates.HalfDepth(s);
            foreach (var site in sites)
            {
                Assert.Less(site.magnitude, SpawnRing - 100f, "a crystal sits out by the spawn ring");
                bool onAxis = false;
                foreach (var st in structures)
                {
                    Vector3 v = site - (st.Centre + st.N * front);
                    float along = Vector3.Dot(v, st.N);
                    if (along >= ShatterplanePlates.CrystalOffset - 1e-2f && (v - st.N * along).magnitude < 1e-2f) onAxis = true;
                }
                Assert.IsTrue(onAxis, $"I{intensity}: a crystal site is not on a plate's axis in front of it");
            }
        }

        // ── Per intensity: I3's team Space crystals, I4's live elemental fauna (Shombith, 2026-11-10) ──

        [Test]
        public void CrystalField_I3IsSpace_OneCrystalPerTeam_ElsewhereAnyonesRandom()
        {
            var settings = ScriptableObject.CreateInstance<ShatterplaneSettingsSO>();
            try
            {
                Assert.AreEqual(Element.Space, settings.FieldElementFor(3), "I3's crystals are Space");
                Assert.IsTrue(settings.FieldPerTeamFor(3), "I3 lays one crystal per team present");
                foreach (int i in new[] { 1, 2, 4 })
                {
                    Assert.AreEqual(Element.None, settings.FieldElementFor(i), $"I{i} draws each site's element at random");
                    Assert.IsFalse(settings.FieldPerTeamFor(i), $"I{i}'s crystals are anyone's");
                }
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void TeamCrystal_IsCollectableByItsTeamAlone()
        {
            var go = new GameObject("team crystal");
            try
            {
                var crystal = go.AddComponent<Crystal>();
                crystal.ApplyDomainPreview(Domains.Ruby);
                Assert.IsTrue(crystal.CanBeCollected(Domains.Ruby), "its own team takes it");
                Assert.IsFalse(crystal.CanBeCollected(Domains.Jade), "another team cannot");
                Assert.IsFalse(crystal.CanBeCollected(Domains.Gold), "another team cannot");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>The spawn profile a shipped cell config points at, and that profile's SupportedFaunas guids.</summary>
        static List<string> FaunaOf(int intensity)
        {
            const string dir = "Assets/_SO_Assets/Cell Configs/Shatterplane Cell";
            string config = File.ReadAllText(Path.Combine(dir, $"Shatterplane Cell Config {intensity}.asset"));
            var m = Regex.Match(config, @"^  SpawnProfile: \{fileID: 11400000, guid: ([0-9a-f]{32})", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"I{intensity}'s cell config names no spawn profile");
            foreach (var path in Directory.GetFiles(dir, "*.asset.meta"))
            {
                if (!File.ReadAllText(path).Contains("guid: " + m.Groups[1].Value)) continue;
                string profile = File.ReadAllText(path.Substring(0, path.Length - ".meta".Length));
                var list = new List<string>();
                var block = Regex.Match(profile, @"^  SupportedFaunas:\s*\n((?:  - [^\n]*\n)*)", RegexOptions.Multiline);
                foreach (Match g in Regex.Matches(block.Groups[1].Value, @"guid: ([0-9a-f]{32})")) list.Add(g.Groups[1].Value);
                return list;
            }
            Assert.Fail($"I{intensity}'s spawn profile is not in {dir}");
            return null;
        }

        [Test]
        public void Fauna_OnlyI4CarriesLiveElementalFauna()
        {
            for (int i = 1; i <= 3; i++) Assert.IsEmpty(FaunaOf(i), $"I{i} carries no wildlife");
            var i4 = FaunaOf(4);
            Assert.AreEqual(2, i4.Count, "I4 carries the tadpole and the shark");
            foreach (var guid in i4)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var config = UnityEditor.AssetDatabase.LoadAssetAtPath<FaunaConfigurationSO>(path);
                Assert.IsNotNull(config, $"I4's fauna {guid} is not a FaunaConfigurationSO");
                Assert.IsTrue(config.SpreadElements, $"{path}: each creature must roll its own element");
                Assert.AreEqual(4, config.ElementPalette.Count, $"{path}: the palette is the four elements");
            }
        }

        // ── The ring: the Brittle Star's own disk rule against one plate ──

        /// <summary>How many breakable tiles of a lone plate a ring takes when it blows at the plate's centre, its disk
        /// tilted <paramref name="tiltDegrees"/> off the plate - the executor's own test, prism by prism.</summary>
        static int RingTakes(ShatterplanePlates.Settings plate, float tiltDegrees, float diskRadius)
        {
            plate.Seed = 1;
            plate.InnerRadius = 0f;
            plate.OuterRadius = 1f;
            plate.StructureCount = 1;
            plate.PlatesPerStack = 1;
            plate.ScreenEvery = 0;
            plate.FaceOut = false;
            var tiles = Lay(plate, out var structures);
            var st = structures[0];
            Vector3 normal = Quaternion.AngleAxis(tiltDegrees, st.W) * st.N;
            const float halfThickness = 0.22f * 4f;   // the ring's strut half-thickness x ringBlastThicknessScale, Charge 0
            Vector3 scale = ShatterplanePlates.TileScale;
            int taken = 0;
            foreach (var t in tiles)
            {
                if (t.Kind == PrismKind.SuperShielded) continue;   // super-shielded refuses (Prism.Damage)
                Vector3 right = t.Rotation * Vector3.right, up = t.Rotation * Vector3.up, fwd = t.Rotation * Vector3.forward;
                float extent = 0.5f * (Mathf.Abs(Vector3.Dot(right, normal)) * scale.x
                                     + Mathf.Abs(Vector3.Dot(up, normal)) * scale.y
                                     + Mathf.Abs(Vector3.Dot(fwd, normal)) * scale.z);
                if (BrittleStarRules.DiskReaches(t.Position - st.Centre, normal, halfThickness, diskRadius, extent, 0f))
                    taken++;
            }
            return taken;
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void Ring_SquareErasesThePlate_TiltedCutsABand(int intensity)
        {
            var s = ReadRung(intensity, out _);
            var tiles = Lay(new ShatterplanePlates.Settings
            {
                Seed = 1, OuterRadius = 1f, StructureCount = 1, PlateRadius = s.PlateRadius, PlatesPerStack = 1,
                RibEvery = s.RibEvery, DangerRim = s.DangerRim,
            }, out _);
            int breakable = 0;
            foreach (var t in tiles) if (t.Kind != PrismKind.SuperShielded) breakable++;

            const float nucleus = 470f;
            Assert.AreEqual(breakable, RingTakes(s, 0f, nucleus), "a square ring takes the whole plate");
            int five = RingTakes(s, 5f, nucleus), twenty = RingTakes(s, 20f, nucleus);
            Assert.Less(five, breakable / 2, "five degrees off square takes under half the plate");
            Assert.Greater(five, twenty, "the squarer the throw, the more it takes");
            Assert.Greater(twenty, 0, "even a bad throw cuts a band");
        }

        // ── The AI's line-up ──

        [Test]
        public void SquareSteer_LinesUpOnTheAxis_ThenFliesIn()
        {
            Vector3 plate = new(0f, 0f, 300f), axis = Vector3.forward;
            // Off to the side: go to the line-up point first.
            Assert.AreEqual(plate + axis * 300f,
                ShatterplaneSquarer.SquareSteer(new Vector3(400f, 0f, 350f), plate, axis, 300f, 10f));
            // Behind the plate: still the line-up point, never through the plate's back.
            Assert.AreEqual(plate + axis * 300f,
                ShatterplaneSquarer.SquareSteer(new Vector3(0f, 0f, 100f), plate, axis, 300f, 10f));
            // On the axis in front, within the square cone: straight in.
            Assert.AreEqual(plate,
                ShatterplaneSquarer.SquareSteer(new Vector3(20f, 0f, 600f), plate, axis, 300f, 10f));
        }

        // ── The scoring ──

        [Test]
        public void Rule_ScoresPrismsDestroyed_SoTheCardIsASpaceGame()
        {
            var rule = ScriptableObject.CreateInstance<ShatterplaneScoringRuleSO>();
            try
            {
                Assert.IsInstanceOf<RampageScoringRuleSO>(rule, "Rampage's rule, inherited");
                Assert.IsTrue(ModeGenre.TryElementsFor(GameModes.Shatterplane, ScoringMetric.PrismsDestroyed,
                    out var genre, out _));
                Assert.AreEqual(Element.Space, genre);
            }
            finally { Object.DestroyImmediate(rule); }
        }

        [Test]
        public void Target_IsPerIntensity_AndFallsBackToTheScalar()
        {
            var eco = ScriptableObject.CreateInstance<EndConditionOverridesSO>();
            try
            {
                Assert.AreEqual(1600, eco.GetShatterplanePrismTarget(1));
                Assert.AreEqual(1600, eco.GetShatterplanePrismTarget(2));
                Assert.AreEqual(2000, eco.GetShatterplanePrismTarget(3));
                Assert.AreEqual(1000, eco.GetShatterplanePrismTarget(4));
                eco.shatterplanePrismTargetByIntensity = new List<int> { 0, 0, 0, 0 };
                eco.shatterplanePrismTarget = 0;
                Assert.AreEqual(EndConditionOverridesSO.DefaultShatterplanePrismTarget, eco.GetShatterplanePrismTarget(3),
                    "a rung with nothing to say falls back to the scalar, never to 0");
                Assert.IsTrue(eco.TryGetAuthoredTurnTarget(GameModes.Shatterplane, out int t) && t > 0);
            }
            finally { Object.DestroyImmediate(eco); }
        }
    }
}

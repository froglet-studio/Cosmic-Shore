#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Cleave's I1-I4 are four rungs of a SPACE game only while these shipped facts hold together (Arcade/CLEAVE.md
    /// "Where Cleave stands"): the game reads as Space alone (so the platform's standard element ladder gives I2
    /// Space 5, I3 team Space crystals and I4 Space fauna), the card authors no starting-element row, each rung has
    /// its own cell and arena, the I4 cell authors no fauna of its own (so it gets the ladder's), and the team
    /// crystals sit inside the mass rather than in the empty moat round it. Each is one edit away from silently
    /// flattening the ladder, so each is read off the shipped asset here.
    /// </summary>
    [TestFixture]
    public class CleaveLadderTests
    {
        const string CardPath = "Assets/_SO_Assets/Games/ArcadeGameCleave.asset";
        const string CellFolder = "Assets/_SO_Assets/Cell Configs/Cleave Cell/";

        /// <summary>The cage's innermost rind (SpawnableRibcage: 720 less two 130 shell gaps), restated.</summary>
        const float CageInnermostRind = 460f;

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"{path} is missing or is not a {typeof(T).Name}");
            return asset;
        }

        static CellConfigDataSO Cell(int intensity) =>
            Load<CellConfigDataSO>($"{CellFolder}Cleave Cell Config {intensity}.asset");

        [Test]
        public void Cleave_IsASpaceGame_SoTheElementLadderLaysSpace()
        {
            Assert.IsTrue(ModeGenre.TryElementsFor(GameModes.Cleave, ScoringMetric.PrismsDestroyed,
                              out var primary, out var secondary));
            Assert.AreEqual(Element.Space, primary, "I2's level 5, I3's team crystals and I4's fauna follow this element");
            Assert.AreEqual(Element.None, secondary, "a two-element game gets no team crystals and no ladder fauna");
        }

        [Test]
        public void ShippedPreview_NamesTheMetricTheLadderReads()
        {
            // ElementLadderCrystals and Cell.LadderFaunaApplies ask the preview library, not the scoring rule.
            Assert.AreEqual(ScoringMetric.PrismsDestroyed, ModePreviewLibrarySO.ObjectiveMetricOf(GameModes.Cleave));
            Assert.IsTrue(ModePreviewLibrarySO.TryGenres(GameModes.Cleave, out var primary, out var secondary));
            Assert.AreEqual(Element.Space, primary);
            Assert.AreEqual(Element.None, secondary);
        }

        [Test]
        public void ShippedCard_OffersFourRungs_SeatsTheSwordfish_AndKeepsTheI2Default()
        {
            var card = Load<SO_ArcadeGame>(CardPath);
            Assert.AreEqual(GameModes.Cleave, card.Mode);
            Assert.AreEqual(1, card.MinIntensity);
            Assert.AreEqual(4, card.MaxIntensity);
            Assert.AreEqual(1, card.Vessels.Count, "the card's list is the vessel lock");
            Assert.AreEqual(VesselClassType.Swordfish, card.Vessels[0].Class, "Cleave is the Swordfish's Space game");
            Assert.IsTrue(card.StartingElements == null || card.StartingElements.Count == 0,
                "any authored starting-element row opts the card out of the I2 Space 5 default on every rung");
        }

        [Test]
        public void EveryRung_IsItsOwnCell_AndItsOwnArena()
        {
            var cells = new HashSet<CellConfigDataSO>();
            var arenas = new HashSet<GameObject>();
            for (int i = 1; i <= 4; i++)
            {
                var cell = Cell(i);
                Assert.IsNotNull(cell.EnvironmentPrefab, $"I{i} has no arena");
                Assert.IsTrue(cells.Add(cell), $"I{i} shares its cell with a lower rung");
                Assert.IsTrue(arenas.Add(cell.EnvironmentPrefab.gameObject),
                    $"I{i} cuts the same arena as a lower rung: intensity is WHICH PLACE you cut");
            }
        }

        [Test]
        public void TheFaunaRungCell_AuthorsNoFauna_SoItGetsTheLaddersSpecies()
        {
            // Cell.FaunaRoster: at I4 a profile with no fauna takes Resources/ElementLadderFauna, pinned to Space.
            var profile = Cell(ElementLadder.FaunaFrom).SpawnProfile;
            Assert.IsNotNull(profile);
            Assert.IsTrue(profile.SupportedFaunas == null || profile.SupportedFaunas.Count == 0,
                "Cleave's cell grows nothing of its own; an authored species would replace the ladder's set");
            var ladder = ElementLadderFaunaSO.Load();
            Assert.IsNotNull(ladder, "Resources/ElementLadderFauna is what I4 hatches");
            Assert.Greater(ladder.Species.Count, 0);
        }

        [Test]
        public void EveryRungsTarget_IsAuthored_AndTheDenseRungsAskForNoLess()
        {
            var ends = EndConditionOverridesSO.Instance;
            Assert.IsNotNull(ends);
            int previous = 0;
            for (int i = 1; i <= 4; i++)
            {
                int target = ends.GetCleavePrismTarget(i);
                Assert.Greater(target, 0, $"I{i} has no destruction target");
                Assert.GreaterOrEqual(target, previous, $"I{i} asks for fewer prisms than the rung below");
                previous = target;
            }
        }

        [Test]
        public void ShippedTargets_AreTheNinetySecondOnes()
        {
            // cleave_budget.TARGET_BY_INTENSITY (2026-11-27): what the all-AI match cut in ~90 s at real speed. Another
            // generator that rewrites EndConditionOverrides.asset can put the old 950 / 1,000 back; this fails then.
            int[] want = { 165, 235, 550, 600 };
            var ends = EndConditionOverridesSO.Instance;
            for (int i = 1; i <= 4; i++)
                Assert.AreEqual(want[i - 1], ends.GetCleavePrismTarget(i), $"I{i}");
        }

        [Test]
        public void TeamCrystals_AreAlwaysInsideTheArena()
        {
            for (int i = ElementLadder.TeamCrystalsFrom; i <= 4; i++)
            {
                CleaveController.LadderCrystalShell(i, out float inner, out float outer);
                Assert.Greater(inner, 0f, $"I{i}: a crystal at the exact centre is one every chord through the ball takes");
                Assert.Less(inner, outer);
                Assert.Less(outer, SliceArenaGeometry.OuterRadiusFor(i),
                    $"I{i}: a crystal outside the arena is a free pickup that asks nothing of the sword");
            }
        }

        [Test]
        public void TheCagesCrystals_AreBehindAllThreeRinds()
        {
            CleaveController.LadderCrystalShell(3, out _, out float outer);
            Assert.Less(outer, CageInnermostRind,
                "I3's verb is peel inward: the Space crystals are the reason to reach the heart");
        }

        [Test]
        public void TheAI_SpendsAMinorityOfStrikesOnCrystals_AndNeverARaidBeat()
        {
            for (int seat = 0; seat < 9; seat++)
            {
                int crystal = 0;
                for (int strike = 0; strike < 120; strike++)
                {
                    if (!CleaveController.IsCrystalStrike(strike, seat)) continue;
                    crystal++;
                    Assert.AreNotEqual(0, (strike + seat) % 4, "a raid beat stays a raid");
                }
                Assert.Greater(crystal, 0, $"seat {seat} never turns for its team's Space crystals");
                Assert.Less(crystal, 60, $"seat {seat} spends most of its strikes inside the arena on crystals");
            }
        }

        // ── Lances (CLEAVE.md "Lances"): one test per placement rule ──

        static List<CleaveLancePart> Lances(int intensity)
        {
            var parts = new List<CleaveLancePart>();
            CleaveLances.Lay(intensity, Vector3.zero, parts);
            return parts;
        }

        [Test]
        public void Lances_StayInsideTheirRungsArena_AndClearOfTheCentre()
        {
            for (int i = 1; i <= 4; i++)
            {
                var rung = CleaveLances.ForIntensity(i);
                float arena = SliceArenaGeometry.OuterRadiusFor(i);
                foreach (var p in Lances(i))
                {
                    float along = Vector3.Dot(p.Position, p.Forward);
                    Assert.GreaterOrEqual(along - CleaveLances.SegmentLength * 0.5f, arena * rung.InnerFraction - 0.01f,
                        $"I{i}: a lance reaches into the centre knot every chord crosses");
                    Assert.LessOrEqual(along + CleaveLances.SegmentLength * 0.5f, arena, $"I{i}: a lance pokes out of its arena");
                }
            }
        }

        [Test]
        public void Lances_AreFlush_NoTwoPrismsOverlap()
        {
            for (int i = 1; i <= 4; i++)
            {
                var parts = Lances(i);
                for (int a = 0; a < parts.Count; a++)
                for (int b = a + 1; b < parts.Count; b++)
                {
                    if (parts[a].Lance != parts[b].Lance) continue;
                    var d = parts[b].Position - parts[a].Position;
                    float alongGap = Mathf.Abs(Vector3.Dot(d, parts[a].Forward));
                    float acrossGap = Vector3.ProjectOnPlane(d, parts[a].Forward).magnitude;
                    bool apart = alongGap >= CleaveLances.SegmentLength - 0.01f || acrossGap >= CleaveLances.Section - 0.01f;
                    Assert.IsTrue(apart, $"I{i}: two lance prisms interpenetrate");
                }
            }
        }

        [Test]
        public void TheSixLances_NeverMeet()
        {
            // Axis lances only meet at the centre, which every rung keeps clear of.
            for (int i = 1; i <= 4; i++)
            {
                var rung = CleaveLances.ForIntensity(i);
                float halfBar = rung.Bundle * CleaveLances.Section * 0.71f;
                Assert.Greater(SliceArenaGeometry.OuterRadiusFor(i) * rung.InnerFraction, halfBar * 2f,
                    $"I{i}: two lances' first segments would cross at the centre");
            }
        }

        [Test]
        public void I1_OnlyHelps_PlainBarsAndNoCost()
        {
            foreach (var p in Lances(1)) Assert.AreEqual(PrismKind.Plain, p.Kind);
            Assert.AreEqual(2, CleaveLances.ForIntensity(1).Bundle);
        }

        [Test]
        public void I2_IsTheWidestBundle_WithDangerOnlyAtTheOuterEnd()
        {
            var rung = CleaveLances.ForIntensity(2);
            Assert.Greater(rung.Bundle, CleaveLances.ForIntensity(1).Bundle, "the Space 5 bill's lesson is a wider bar");
            int segments = CleaveLances.SegmentsFor(2);
            foreach (var p in Lances(2))
            {
                bool tip = p.Segment >= segments - rung.DangerTips;
                Assert.AreEqual(tip ? PrismKind.Danger : PrismKind.Plain, p.Kind, $"segment {p.Segment}");
            }
        }

        [Test]
        public void I3_IsArmouredInTheCagesHeart()
        {
            foreach (var p in Lances(3))
            {
                Assert.AreEqual(PrismKind.SuperShielded, p.Kind, "Shield Breaker's lesson: the heart is armour");
                Assert.Less(p.Position.magnitude + CleaveLances.SegmentLength, CageInnermostRind,
                    "the heart's lances stay inside the innermost rind");
            }
        }

        [Test]
        public void I4_IsASeam_ArmourAlternatesWithPlain_AndTheTipsAreDanger()
        {
            var rung = CleaveLances.ForIntensity(4);
            int segments = CleaveLances.SegmentsFor(4);
            int plain = 0, armour = 0;
            foreach (var p in Lances(4))
            {
                bool tip = p.Segment >= segments - rung.DangerTips;
                if (tip) { Assert.AreEqual(PrismKind.Danger, p.Kind); continue; }
                Assert.AreEqual(p.Segment % 2 == 1 ? PrismKind.SuperShielded : PrismKind.Plain, p.Kind);
                if (p.Kind == PrismKind.Plain) plain++; else armour++;
            }
            Assert.Greater(plain, 0, "the plain segments still feed the ramp");
            Assert.Greater(armour, 0);
        }

        [Test]
        public void Lances_ScoreForEveryTeam_NoColourOwnsMoreThanAQuarter()
        {
            for (int i = 1; i <= 4; i++)
            {
                var counts = new Dictionary<Domains, int>();
                var parts = Lances(i);
                foreach (var p in parts) counts[p.Domain] = counts.TryGetValue(p.Domain, out var n) ? n + 1 : 1;
                Assert.IsTrue(counts.ContainsKey(Domains.Blue), "Blue is no team's, so part of every lance scores for all");
                foreach (var kv in counts)
                    Assert.LessOrEqual(kv.Value, parts.Count / 4 + parts.Count / 20,
                        $"I{i}: {kv.Key} owns too much of the lances, so that team scores little of them");
            }
        }

        [Test]
        public void Lances_AreTheSameOnEveryPeer()
        {
            for (int i = 1; i <= 4; i++)
            {
                var a = Lances(i);
                var b = Lances(i);
                Assert.AreEqual(a.Count, b.Count);
                for (int k = 0; k < a.Count; k++)
                {
                    Assert.AreEqual(a[k].Position, b[k].Position);
                    Assert.AreEqual(a[k].Kind, b[k].Kind);
                    Assert.AreEqual(a[k].Domain, b[k].Domain);
                }
                Assert.AreEqual(CleaveLances.PartCountFor(i), a.Count);
            }
        }

        [Test]
        public void ArenaPlusLances_StaysUnderTheCollidersTheModeShipped()
        {
            // cleave_budget.HISTORICAL_COLLIDER_CEILING; the arena counts are cleave_arena_measurements.json's.
            const int ceiling = 20153;
            int[] arena = { 15380, 14277, 14731, 16423 };
            for (int i = 1; i <= 4; i++)
                Assert.LessOrEqual(arena[i - 1] + CleaveLances.PartCountFor(i), ceiling, $"I{i}");
        }

        [Test]
        public void ScatteredCrystals_LandInTheShell()
        {
            var points = new List<Vector3>();
            for (int i = ElementLadder.TeamCrystalsFrom; i <= 4; i++)
            {
                points.Clear();
                CleaveController.LadderCrystalShell(i, out float inner, out float outer);
                ElementLadderCrystals.ScatterInShell(Vector3.zero, inner, outer, 64, new System.Random(i), points);
                Assert.AreEqual(64, points.Count);
                foreach (var p in points)
                {
                    Assert.GreaterOrEqual(p.magnitude, inner - 0.01f);
                    Assert.LessOrEqual(p.magnitude, outer + 0.01f);
                }
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Palisade's I1-I4 are four rungs only while three shipped facts hold together (Arcade/PALISADE.md section 4):
    /// the game reads as a MASS game (so the platform's standard element ladder gives I2 Mass 5, I3 team Mass crystals
    /// and I4 Mass fauna), the card authors no starting-element row (one row opts the whole card out of the I2
    /// default), and the mode's own crystal scatter thins rung by rung. Each is one edit away from silently flattening
    /// the ladder, so each is read off the shipped asset here.
    /// </summary>
    [TestFixture]
    public class PalisadeLadderTests
    {
        const string CardPath = "Assets/_SO_Assets/Games/ArcadeGamePalisade.asset";
        const string SettingsPath = "Assets/_SO_Assets/Games/PalisadeSettings.asset";
        const string RulePath = "Assets/_SO_Assets/Scoring Rules/PalisadeScoringRule.asset";

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"{path} is missing or is not a {typeof(T).Name}");
            return asset;
        }

        [Test]
        public void Palisade_IsAMassGame_SoTheElementLadderLaysMass()
        {
            Assert.IsTrue(ModeGenre.TryElementsFor(GameModes.Palisade, ScoringMetric.VolumeRemaining,
                              out var primary, out var secondary));
            Assert.AreEqual(Element.Mass, primary, "I2's level 5, I3's team crystals and I4's fauna follow this element");
            Assert.AreEqual(Element.None, secondary, "a two-element game gets no team crystals and no ladder fauna");
        }

        [Test]
        public void ShippedPreview_NamesTheMetricTheLadderReads()
        {
            // ElementLadderCrystals and Cell.LadderFaunaApplies ask the preview library, not the scoring rule.
            Assert.AreEqual(ScoringMetric.VolumeRemaining, ModePreviewLibrarySO.ObjectiveMetricOf(GameModes.Palisade));
            Assert.IsTrue(ModePreviewLibrarySO.TryGenres(GameModes.Palisade, out var primary, out var secondary));
            Assert.AreEqual(Element.Mass, primary);
            Assert.AreEqual(Element.None, secondary);
        }

        [Test]
        public void ShippedCard_OffersFourRungs_AndKeepsTheI2Default()
        {
            var card = Load<SO_ArcadeGame>(CardPath);
            Assert.AreEqual(GameModes.Palisade, card.Mode);
            Assert.AreEqual(1, card.MinIntensity);
            Assert.AreEqual(4, card.MaxIntensity);
            Assert.AreEqual(1, card.Vessels.Count, "Swordfish only: the card's list is the vessel lock");
            Assert.IsTrue(card.StartingElements == null || card.StartingElements.Count == 0,
                "any authored starting-element row opts the card out of the I2 Mass 5 default on every rung");
        }

        [Test]
        public void ShippedScatter_ThinsOnEveryRung()
        {
            var settings = Load<PalisadeSettingsSO>(SettingsPath);
            var counts = settings.elementalCrystalCountByIntensity;
            Assert.AreEqual(4, counts.Length, "one count per intensity");
            for (int i = 0; i < counts.Length; i++)
            {
                Assert.Greater(counts[i], 0, $"I{i + 1} lays no crystals");
                if (i > 0) Assert.Less(counts[i], counts[i - 1], $"I{i + 1} is not scarcer than I{i}");
            }
        }

        [Test]
        public void CrystalCountFor_ClampsOutsideTheLadder()
        {
            var settings = ScriptableObject.CreateInstance<PalisadeSettingsSO>();
            try
            {
                settings.elementalCrystalCountByIntensity = new[] { 24, 16, 10, 6 };
                Assert.AreEqual(24, settings.CrystalCountFor(0));
                Assert.AreEqual(24, settings.CrystalCountFor(1));
                Assert.AreEqual(10, settings.CrystalCountFor(3));
                Assert.AreEqual(6, settings.CrystalCountFor(4));
                Assert.AreEqual(6, settings.CrystalCountFor(9));

                settings.elementalCrystalCountByIntensity = new int[0];
                Assert.AreEqual(0, settings.CrystalCountFor(2), "an empty ladder lays nothing rather than throwing");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ShippedAI_CanReachACrystal_AndRaidsBeforeTheWhistle()
        {
            var settings = Load<PalisadeSettingsSO>(SettingsPath);
            Assert.Greater(settings.aiCrystalReach, 0f, "a builder that never detours never takes its team's Mass crystals");
            Assert.Greater(settings.aiLateRaidSeconds, settings.aiDecisionSeconds,
                "the late raid must outlast one decision tick or an AI can miss it entirely");
        }

        [Test]
        public void ShippedRule_IsPalisades_OnStandingVolume_HighestWins()
        {
            var rule = Load<PalisadeScoringRuleSO>(RulePath);
            Assert.AreEqual(ScoringMetric.VolumeRemaining, rule.Metric);
            Assert.IsFalse(rule.GolfRules, "the most volume standing wins");
        }
    }
}
#endif

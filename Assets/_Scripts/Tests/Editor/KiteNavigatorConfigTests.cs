#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The shipped <c>KiteNavigatorConfig.asset</c> is the Crew Vessel Lab's signed-off Kite &amp; Tail candidate (round 3,
    /// 2026-10-17): the lab is the record and the asset its clone. <c>Tools/Build/kite_navigator_lab/parity.cjs</c> diffs
    /// the asset against the page key by key; this test pins the same numbers inside Unity, plus the wiring a reader of
    /// the YAML cannot see go wrong (the blast prefab, the score channel, the game data).
    /// </summary>
    public class KiteNavigatorConfigTests
    {
        const string Path = "Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset";

        static KiteNavigatorConfigSO Load()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<KiteNavigatorConfigSO>(Path);
            Assert.IsNotNull(cfg, $"no KiteNavigatorConfigSO at {Path}");
            return cfg;
        }

        [Test]
        public void Asset_CarriesTheLabsSignedOffNumbers()
        {
            var c = Load();
            Assert.AreEqual(160f, c.BoardRadius);
            Assert.AreEqual(0.3f, c.Anchor, 1e-6f);
            Assert.AreEqual(KiteBoardFrame.Eased, c.Frame);
            Assert.AreEqual(3f, c.EaseRate);
            Assert.AreEqual(40f, c.ReachY);
            Assert.AreEqual(8f, c.KiteRadius);
            Assert.AreEqual(21f, c.ThumbOffsetMillimetres, "the lab's 80 CSS px at 96 per inch");
            Assert.AreEqual(KiteMoteHome.Board, c.MoteHome);
            Assert.AreEqual(26, c.MoteCount);
            Assert.AreEqual(4, c.MoteCluster);
            Assert.AreEqual(18f, c.MoteSpread);
            Assert.AreEqual(KiteMixedLoopRule.Fizzle, c.MixedLoops);
            Assert.AreEqual(2f, c.Growth);
            Assert.AreEqual(60f, c.SpaceMaxScale);
            Assert.AreEqual(640f, c.SpaceMaxScaleCap);
            Assert.AreEqual(6f, c.MassPrisms);
            Assert.AreEqual(240f, c.MassPrismCap);
            Assert.AreEqual(new Vector3(2f, 2f, 5f), c.MassPrismScale, "the Sparrow's trail prism");
            Assert.AreEqual(1.5f, c.TimeBoostSeconds);
            Assert.AreEqual(30f, c.TimeBoostCap);
            Assert.AreEqual(1f, c.ChargePetals);
            Assert.AreEqual(30f, c.ChargeCap);
            Assert.AreEqual(1f, c.StealCooldown);
            Assert.AreEqual(KiteNavigatorAI.Skill.Hard, c.AISkill);
        }

        [Test]
        public void Settings_AreTheNumbersTheParityHarnessHoldsTheCoreTo()
        {
            var s = Load().ToSettings();
            Assert.AreEqual(160f, s.BoardRadius);
            Assert.AreEqual(0.3f, s.Anchor, 1e-6f);
            Assert.AreEqual(320f, s.KiteSpeed);
            Assert.AreEqual(12f, s.KiteAccel);
            Assert.AreEqual(1.6f, s.TailLength, 1e-6f);
        }

        [Test]
        public void Asset_IsWiredToTheGamesBlastScoreAndPlayers()
        {
            var c = Load();
            Assert.IsTrue(c.SpaceExplosions is { Length: 1 } && c.SpaceExplosions[0], "Space detonates the game's AOEExplosion");
            Assert.AreEqual("AOEExplosion", c.SpaceExplosions[0].name);
            Assert.IsTrue(c.OnCombatHitLanded, "a ram is a scored combat hit, raised on the scoreboard's channel");
            Assert.IsTrue(c.GameData, "the board finds rival hulls through the runtime game data");
        }
    }
}
#endif

#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
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
            // Garrett's 2026-10-18 direction, built in Unity first (KITE_NAVIGATOR.md "Round 4")
            Assert.AreEqual(2f, c.BoostCooldownSeconds, "a boost waits 2 s after the last, none at Time L5");
            Assert.AreEqual(6, c.OmniMotesPerElement, "round 4e: an Omni crystal drops a cluster of each element, not a zoom");
            Assert.AreEqual(0.5f, c.OmniClusterRadius, 1e-6f);
            Assert.AreEqual(1f, c.MoteBaseWeight);
            Assert.AreEqual(1f, c.MoteWeightPerLevel);
            Assert.AreEqual(1f, c.FaunaChargeCost);
            // round 4d: out-of-plane prisms are seen, with depth from the plane and a dither cone above it
            Assert.AreEqual(400f, c.ViewDepth);
            Assert.AreEqual(60f, c.DepthFullSpreadAt);
            Assert.AreEqual(0.35f, c.DitherCoreAlpha, 1e-6f);
            Assert.AreEqual(0.5f, c.DitherNearScale, 1e-6f);
            // round 4e: the petals set apart, Space spinning without pausing, Mass +20%, Charge's plasma visible
            Assert.AreEqual(0.5f, c.PetalGap, 1e-6f);
            Assert.AreEqual(0.65f, c.PetalWidth, 1e-6f);
            Assert.AreEqual(150f, c.SpaceSpinDegreesPerSecond);
            Assert.AreEqual(0.72f, c.MassPulse, 1e-6f, "round 4d's 0.6, +20%");
            Assert.AreEqual(1.35f, c.ChargeHaloScale, 1e-6f);
            Assert.AreEqual(0.32f, c.ChargeSparkSize, 1e-6f);
        }

        [Test]
        public void Settings_AreTheNumbersTheParityHarnessHoldsTheCoreTo()
        {
            var s = Load().ToSettings();
            Assert.AreEqual(160f, s.BoardRadius);
            Assert.AreEqual(0.3f, s.Anchor, 1e-6f);
            Assert.AreEqual(320f, s.KiteSpeed);
            Assert.AreEqual(12f, s.KiteAccel);
            Assert.AreEqual(2.4f, s.TailLength, 1e-6f, "round 3's 1.6 + 50% (Garrett, 2026-10-18)");
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

        [Test]
        public void KiteHull_CarriesTheSeat_WiredToThisConfig_SoTheVesselChangerOffersAPlayableKite()
        {
            // "Bring it to Unity" = the Toybox's Vessel Changer offers it (Garrett, 2026-10-17); the changer offers hulls,
            // so the seat ships on its own hull (Tools/Build/author_kite_assets.py)
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Spacevessels/Kite.prefab");
            Assert.IsNotNull(prefab, "no Kite.prefab: run Tools/Build/author_kite_assets.py");
            Assert.IsTrue(prefab.TryGetComponent(out KiteNavigator seat), "the Kite's root carries no KiteNavigator");
            var wired = new SerializedObject(seat).FindProperty("config").objectReferenceValue;
            Assert.AreEqual(Load(), wired, "the seat is not wired to KiteNavigatorConfig.asset (a null config never seats the navigator)");
            Assert.IsTrue(prefab.TryGetComponent(out IVesselStatus status));
            Assert.AreEqual(VesselClassType.Kite, status.VesselType);
            CollectionAssert.Contains(ToyVesselRoster.Default, VesselClassType.Kite, "the Vessel Changer offers only the roster");
        }

        [Test]
        public void KiteHull_FliesOnTwoSticks_AndItsHudReadsTheSeat()
        {
            // round 4d (Garrett, 2026-10-18): "lets go back to two thumb flying for the pilot", and the ability cards show
            // the saved boosts, the charges, the last blast and the Mass flash
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Spacevessels/Kite.prefab");
            Assert.IsNotNull(prefab);
            Assert.IsTrue(prefab.TryGetComponent(out VesselTransformer transformer));
            Assert.AreEqual(typeof(VesselTransformer), transformer.GetType(), "the Kite's hull is not the two-stick VesselTransformer");
            Assert.IsTrue(prefab.TryGetComponent(out KiteHUDController hud), "the Kite's HUD controller is not KiteHUDController");
            Assert.AreEqual(prefab.GetComponent<KiteNavigator>(), new SerializedObject(hud).FindProperty("navigator").objectReferenceValue,
                "the HUD does not read this hull's seat");
        }
    }
}
#endif

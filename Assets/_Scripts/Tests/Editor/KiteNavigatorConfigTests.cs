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
            Assert.AreEqual(KiteBoardFrame.Heading, c.Frame, "round 7: a yaw in place does not drag the board");
            Assert.AreEqual(220f, c.HeadingAlignDistance);
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
            // rounds 4e-4f: the totem's proportions, Space spinning without pausing, Mass +20% with three petals held in,
            // Charge's one bolt round the whole flower
            Assert.AreEqual(4.5f, c.MoteSize);
            Assert.AreEqual(150f, c.SpaceSpinDegreesPerSecond);
            Assert.AreEqual(0.72f, c.MassPulse, 1e-6f, "round 4d's 0.6, +20%");
            Assert.AreEqual(2, c.MassPetalsOut, "three of the five held in");
            Assert.AreEqual(0.5f, c.MassStepSeconds, 1e-6f);
            Assert.AreEqual(1.6f, c.ChargeBoltLapSeconds, 1e-6f);
            Assert.AreEqual(0.22f, c.ChargeSparkSize, 1e-6f);
            Assert.AreEqual(7, c.ChargeBoltTrail);
        }

        [Test]
        public void Asset_CarriesTheScreenLayout()
        {
            var c = Load();
            // round 7 (Garrett, 2026-10-20): the whole screen, zoomed out 30%, the cord drawn
            Assert.IsTrue(c.WholeScreen);
            Assert.AreEqual(1.352f, c.ViewScale, 1e-6f, "1.04 x 1.3");
            Assert.AreEqual(0.6f, c.CordWidth, 1e-6f);
            Assert.AreEqual(0.75f, c.CordAlpha, 1e-6f);
            // round 8: the hand line above the totems, the kite off the bottom, the pilot's larger stand-in
            Assert.AreEqual(0.012f, c.HandLineGap, 1e-6f);
            Assert.AreEqual(0.2f, c.HandLineFallback, 1e-6f);
            Assert.AreEqual(40f, c.OffscreenOverhang, "Tools/Build/author_wrasse_run_assets.py sizes the race clamp to it");
            Assert.AreEqual(2.5f, c.PilotKiteScale, 1e-6f);
        }

        [Test]
        public void Settings_AreTheNumbersTheParityHarnessHoldsTheCoreTo()
        {
            var s = Load().ToSettings();
            Assert.AreEqual(160f, s.BoardRadius);
            Assert.AreEqual(0.3f, s.Anchor, 1e-6f);
            Assert.AreEqual(320f, s.KiteSpeed);
            Assert.AreEqual(12f, s.KiteAccel);
            Assert.AreEqual(4.8f, s.TailLength, 1e-6f, "round 3's 1.6 + 50% (Garrett, 2026-10-18), doubled (2026-10-20)");
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
        public void WrasseHull_CarriesTheSeat_WiredToThisConfig_SoTheVesselChangerOffersAPlayableKite()
        {
            // "Bring it to Unity" = the Toybox's Vessel Changer offers it (Garrett, 2026-10-17); the changer offers hulls,
            // so the seat ships on its own hull (Tools/Build/author_wrasse_assets.py)
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Spacevessels/Wrasse.prefab");
            Assert.IsNotNull(prefab, "no Wrasse.prefab: run Tools/Build/author_wrasse_assets.py");
            Assert.IsTrue(prefab.TryGetComponent(out KiteNavigator seat), "the Wrasse's root carries no KiteNavigator");
            var wired = new SerializedObject(seat).FindProperty("config").objectReferenceValue;
            Assert.AreEqual(Load(), wired, "the seat is not wired to KiteNavigatorConfig.asset (a null config never seats the navigator)");
            Assert.IsTrue(prefab.TryGetComponent(out IVesselStatus status));
            Assert.AreEqual(VesselClassType.Wrasse, status.VesselType);
            CollectionAssert.Contains(ToyVesselRoster.Default, VesselClassType.Wrasse, "the Vessel Changer offers only the roster");
        }

        [Test]
        public void WrasseCard_NamesBothSubvessels()
        {
            // Garrett, 2026-10-18: "so its card will read wrasse and ___. pick a fish that the wrasse cleans"
            var cls = AssetDatabase.LoadAssetAtPath<SO_Vessel>("Assets/_SO_Assets/Classes/SO_Class_Wrasse.asset");
            Assert.IsNotNull(cls);
            Assert.AreEqual("Wrasse", cls.Name, "Name stays the vessel's key (the hangar's cloud unlocks)");
            Assert.AreEqual("Wrasse and Grouper", cls.CardName);

            var solo = ScriptableObject.CreateInstance<SO_Vessel>();
            solo.Name = "Sparrow";
            Assert.AreEqual("Sparrow", solo.CardName, "a one-seat vessel's card reads its Name");
            solo.CrewNames = new[] { "A", "B" };
            solo.CrewNameTemplate = string.Empty;
            Assert.AreEqual("Sparrow", solo.CardName, "an empty template shows Name alone");
            Object.DestroyImmediate(solo);
        }

        [Test]
        public void WrasseHull_FliesOnTwoSticks_AndItsHudReadsTheSeat()
        {
            // round 4d (Garrett, 2026-10-18): "lets go back to two thumb flying for the pilot", and the ability cards show
            // the saved boosts, the charges, the last blast and the Mass flash
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Spacevessels/Wrasse.prefab");
            Assert.IsNotNull(prefab);
            Assert.IsTrue(prefab.TryGetComponent(out VesselTransformer transformer));
            Assert.AreEqual(typeof(VesselTransformer), transformer.GetType(), "the Wrasse's hull is not the two-stick VesselTransformer");
            Assert.IsTrue(prefab.TryGetComponent(out WrasseHUDController hud), "the Wrasse's HUD controller is not WrasseHUDController");
            Assert.AreEqual(prefab.GetComponent<KiteNavigator>(), new SerializedObject(hud).FindProperty("navigator").objectReferenceValue,
                "the HUD does not read this hull's seat");
        }
    }
}
#endif

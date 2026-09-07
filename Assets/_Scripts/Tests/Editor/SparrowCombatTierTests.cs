#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The gate for <b>what a Sparrow hit is worth</b>, and for the two wiring facts that decide
    /// whether it is worth anything at all.
    ///
    /// <para>One skyburst reaches a pilot through three concentric radii — the warhead shockwave
    /// (25x the round's hit radius), the prism blast, and the round's own hit sphere — and a
    /// victim inside the inner one is always inside the outer ones. The three
    /// <see cref="CombatHitClass"/> members that name them are therefore <b>ranked, not
    /// additive</b>: <c>VesselCombatHitLatch</c> folds all three onto one window per victim and
    /// pays the best tier achieved. These tests pin that arithmetic, because getting it wrong is
    /// silent — a centre-punch simply pays 60 instead of 30 and nothing complains.</para>
    ///
    /// <para>They also pin the two asset facts the arithmetic rides on: every tier carries the
    /// class its name claims (the class is AUTHORED, nothing re-derives it at runtime), and the
    /// Sparrow's guns sweep for vessels (without which a bullet scores nothing at all, which is
    /// how this pass started).</para>
    ///
    /// <para>Under an <c>Editor/</c> folder per CLAUDE.md — a test anywhere else compiles into
    /// the player and breaks the Windows build at the IL2CPP linker.</para>
    /// </summary>
    public class SparrowCombatTierTests
    {
        const string DirectEffect =
            "Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByMissileDirect.asset";
        const string BlastEffect =
            "Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByMissileBlast.asset";
        const string ShockwaveEffect =
            "Assets/_SO_Assets/Effects/Vessel Explosion Effects/VesselCombatHitByMissileShockwave.asset";
        const string BulletEffect =
            "Assets/_SO_Assets/Effects/Vessel Projectile Effects/VesselCombatHitByBullet.asset";

        const string SkyBurstProjectileContainer =
            "Assets/_SO_Assets/Effects/Effect Containers/Projectile Containers/" +
            "SparrowSkyBurstProjectileImpactContainer.asset";
        const string SkyBurstExplosionContainer =
            "Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/" +
            "SkyBurstExplosionImpactorDataContainer.asset";
        const string WarheadExplosionContainer =
            "Assets/_SO_Assets/Effects/Effect Containers/Explosion Containers/" +
            "MissileWarheadExplosionImpactorDataContainer.asset";
        const string FullAutoContainer =
            "Assets/_SO_Assets/Effects/Effect Containers/Projectile Containers/" +
            "SparrowFullAutoProjectileImpactContainer.asset";
        const string TurretPrismContainer =
            "Assets/_SO_Assets/Effects/Effect Containers/Projectile Containers/" +
            "SparrowPrismProjectileImpactContainer.asset";

        const string ScoringRule = "Assets/_SO_Assets/Scoring Rules/DogFightScoringRule.asset";
        const string BulletPrefab = "Assets/_Prefabs/Projectile/SparrowProjectile.prefab";
        const string TurretPrismPrefab =
            "Assets/_Prefabs/Trails/Prisms With Pools/Sparrow Projectile Prism.prefab";
        const string SparrowVessel = "Assets/_Prefabs/Spacevessels/Sparrow.prefab";
        const string SkyBurstAction = "Assets/_SO_Assets/VesselActions/Sparrow/SkyBurstGunAction.asset";
        const string SparrowHud = "Assets/_Prefabs/UI Elements/VesselHUD/SparrowHUDVariant.prefab";

        static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), $"missing asset: {path}");
            return File.ReadAllText(path);
        }

        static string Guid(string assetPath)
        {
            var m = Regex.Match(Read(assetPath + ".meta"), @"^guid: (\w+)", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"no guid in {assetPath}.meta");
            return m.Groups[1].Value;
        }

        static float Field(string yaml, string key)
        {
            var m = Regex.Match(yaml, @"^\s*" + Regex.Escape(key) + @":\s*(-?[0-9.eE+]+)\s*$",
                                RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"'{key}' is not authored");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        #region The tier assets carry the class their name claims

        // The class is data, not inference: the SAME effect script sits in the bullet container
        // marked 0 and in the skyburst container marked 1. A transposed pair here would price the
        // common outcome as the rare one and produce no other symptom anywhere.
        [Test]
        public void EachTierAssetCarriesItsOwnHitClass()
        {
            Assert.AreEqual((int)CombatHitClass.Bullet, (int)Field(Read(BulletEffect), "hitClass"));
            Assert.AreEqual((int)CombatHitClass.MissileDirect, (int)Field(Read(DirectEffect), "hitClass"));
            Assert.AreEqual((int)CombatHitClass.MissileBlast, (int)Field(Read(BlastEffect), "hitClass"));
            Assert.AreEqual((int)CombatHitClass.MissileShockwave,
                            (int)Field(Read(ShockwaveEffect), "hitClass"));
        }

        // All three tiers contend for ONE latch window per victim. Give any of them a different
        // window and the ranked-upgrade guarantee evaporates - a rocket whose shockwave latched
        // for 0.5s and whose blast latched for 0.2s can pay twice for one victim.
        [Test]
        public void TheThreeMissileTiersShareOneLatchWindow()
        {
            float direct = Field(Read(DirectEffect), "sameVictimCooldownSeconds");
            float blast = Field(Read(BlastEffect), "sameVictimCooldownSeconds");
            float shock = Field(Read(ShockwaveEffect), "sameVictimCooldownSeconds");

            Assert.Greater(direct, 0f, "a zero window disables the latch and every tier pays");
            Assert.AreEqual(direct, blast, 1e-4f);
            Assert.AreEqual(direct, shock, 1e-4f);
        }

        [Test]
        public void EveryTierIsWiredIntoTheThingThatProducesIt()
        {
            StringAssert.Contains(Guid(DirectEffect), Read(SkyBurstProjectileContainer),
                                  "a direct rocket strike would never score");
            StringAssert.Contains(Guid(BlastEffect), Read(SkyBurstExplosionContainer),
                                  "a rocket's prism blast would never score");
            StringAssert.Contains(Guid(ShockwaveEffect), Read(WarheadExplosionContainer),
                                  "the shockwave - the ORDINARY outcome of a proximity kill - " +
                                  "would never score");
            StringAssert.Contains(Guid(BulletEffect), Read(FullAutoContainer));
            StringAssert.Contains(Guid(BulletEffect), Read(TurretPrismContainer),
                                  "the turret-stance prism round is the same weapon class as a " +
                                  "bullet and must score like one");
        }

        #endregion

        #region The price list

        // The four classes Dog Fight has an opinion about. Everything else in the enum must be
        // worth 0 - asserted by enumeration below, not by naming the leftovers.
        static readonly CombatHitClass[] PricedClasses =
        {
            CombatHitClass.Bullet,
            CombatHitClass.MissileShockwave,
            CombatHitClass.MissileBlast,
            CombatHitClass.MissileDirect,
        };

        [Test]
        public void DogFightPricesTheTiersByProximity()
        {
            string rule = Read(ScoringRule);
            Assert.AreEqual(1f, Field(rule, "bulletPoints"));
            Assert.AreEqual(10f, Field(rule, "missileShockwavePoints"));
            Assert.AreEqual(20f, Field(rule, "missileBlastPoints"));
            Assert.AreEqual(30f, Field(rule, "missileDirectPoints"));
        }

        // The three missile classes a single rocket can land. Named here because "which classes
        // are tiers of ONE event" is the law itself, not a case list - everything else is
        // derived from the enum below so a member added later is covered on the day it lands.
        static readonly CombatHitClass[] MissileTiersClosestLast =
        {
            CombatHitClass.MissileShockwave,
            CombatHitClass.MissileBlast,
            CombatHitClass.MissileDirect,
        };

        static CombatHitClass[] AllClasses() =>
            (CombatHitClass[])Enum.GetValues(typeof(CombatHitClass));

        // The ranks are what let the latch upgrade a claim, and the ordering is the whole reason
        // there are three classes rather than one.
        //
        // The "everything else" half ENUMERATES the enum rather than naming Bullet and Debuff,
        // per the rule bleeding-edge records in the ship skill: a guard that lists the members it
        // knows about stops guarding the law the day a member is added, and nothing fails to say
        // so. A sixth class that quietly ranked non-zero would join the missile latch entry and
        // suppress a real rocket tier; this test now fails the moment it does.
        [Test]
        public void ProximityRankOrdersTheTiersAndIgnoresEverythingElse()
        {
            for (int i = 1; i < MissileTiersClosestLast.Length; i++)
                Assert.Less(CombatHitClasses.MissileProximityRank(MissileTiersClosestLast[i - 1]),
                            CombatHitClasses.MissileProximityRank(MissileTiersClosestLast[i]),
                            $"{MissileTiersClosestLast[i - 1]} must rank further out than " +
                            $"{MissileTiersClosestLast[i]}");

            foreach (var hitClass in AllClasses())
            {
                bool isTier = Array.IndexOf(MissileTiersClosestLast, hitClass) >= 0;
                Assert.AreEqual(isTier, CombatHitClasses.MissileProximityRank(hitClass) > 0,
                                $"{hitClass} ranks as a missile tier: {!isTier}");
                Assert.AreEqual(isTier, CombatHitClasses.IsMissile(hitClass),
                                $"{hitClass} reports IsMissile: {!isTier}");
            }
        }

        // NEGATIVE CONTROL for the trap CLAUDE.md records twice: a rule written as
        // `hitClass == X ? a : b` prices every member added later as the default arm, which is
        // how The Bends' Debuff class was once paid at the bullet rate. Dog Fight's rule is an
        // exhaustive switch, so a class it has no opinion about must be worth exactly 0 - not
        // the bullet price, and not the missile price.
        [Test]
        public void AClassTheRuleHasNoOpinionAboutIsWorthZero()
        {
            var rule = ScriptableObject.CreateInstance<DogFightScoringRuleSO>();
            try
            {
                // Enumerated, not named: the priced set is the law, so every OTHER member of the
                // enum - including one added after this was written - must be worth exactly 0.
                foreach (var hitClass in AllClasses())
                {
                    if (Array.IndexOf(PricedClasses, hitClass) >= 0) continue;
                    Assert.AreEqual(0, rule.PointsForCombatHit(hitClass),
                                    $"{hitClass} is not on Dog Fight's price list and must be " +
                                    "worth 0, not the default arm of a two-way test");
                }

                Assert.AreEqual(0, rule.PointsForCombatHit((CombatHitClass)999));
            }
            finally { UnityEngine.Object.DestroyImmediate(rule); }
        }

        #endregion

        #region The upgrade arithmetic — one rocket pays its best tier, exactly once

        static DogFightScoringRuleSO ShippedRule()
        {
            // Built from the SHIPPED numbers rather than the C# field defaults, so a retune of
            // the asset moves these tests with it instead of leaving them asserting a value
            // nothing plays with.
            string yaml = Read(ScoringRule);
            var rule = ScriptableObject.CreateInstance<DogFightScoringRuleSO>();
            var t = typeof(DogFightScoringRuleSO);
            foreach (var pair in new[]
                     {
                         ("bulletPoints", "bulletPoints"),
                         ("missileShockwavePoints", "missileShockwavePoints"),
                         ("missileBlastPoints", "missileBlastPoints"),
                         ("missileDirectPoints", "missileDirectPoints"),
                     })
            {
                var f = t.GetField(pair.Item1, System.Reflection.BindingFlags.Instance |
                                               System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(f, $"{pair.Item1} is not a field on DogFightScoringRuleSO");
                f.SetValue(rule, (int)Field(yaml, pair.Item2));
            }
            return rule;
        }

        [Test]
        public void ARocketThatOnlyGrazesPaysTheShockwave()
        {
            var rule = ShippedRule();
            var stats = new FakeRoundStats();
            try
            {
                CombatHitScoring.Credit(stats, CombatHitClass.MissileShockwave, rule);
                Assert.AreEqual(10, stats.CombatPoints);
                Assert.AreEqual(1, stats.MissileHitsLanded);
            }
            finally { UnityEngine.Object.DestroyImmediate(rule); }
        }

        // The case the whole upgrade rule exists for. The warhead is both the largest radius and
        // the fastest to expand, so on an ordinary kill the CHEAPEST tier lands FIRST; under
        // first-wins a centre-punch would be paid as a graze.
        [Test]
        public void ACentrePunchPaysThirtyInTotal_NotSixty()
        {
            var rule = ShippedRule();
            var stats = new FakeRoundStats();
            try
            {
                CombatHitScoring.Credit(stats, CombatHitClass.MissileShockwave, rule);
                CombatHitScoring.Credit(stats, CombatHitClass.MissileBlast, rule, supersededRank: 1);
                CombatHitScoring.Credit(stats, CombatHitClass.MissileDirect, rule, supersededRank: 2);

                Assert.AreEqual(30, stats.CombatPoints, "the three tiers must not be additive");
                Assert.AreEqual(1, stats.MissileHitsLanded,
                                "an upgrade is the SAME rocket arriving closer - counting it " +
                                "again reports three missile hits for one rocket");
            }
            finally { UnityEngine.Object.DestroyImmediate(rule); }
        }

        [Test]
        public void UpgradingStraightToADirectHitAlsoTotalsThirty()
        {
            var rule = ShippedRule();
            var stats = new FakeRoundStats();
            try
            {
                CombatHitScoring.Credit(stats, CombatHitClass.MissileShockwave, rule);
                CombatHitScoring.Credit(stats, CombatHitClass.MissileDirect, rule, supersededRank: 1);
                Assert.AreEqual(30, stats.CombatPoints);
                Assert.AreEqual(1, stats.MissileHitsLanded);
            }
            finally { UnityEngine.Object.DestroyImmediate(rule); }
        }

        [Test]
        public void BulletsAndDebuffsAreCountedSeparatelyFromMissiles()
        {
            var rule = ShippedRule();
            var stats = new FakeRoundStats();
            try
            {
                CombatHitScoring.Credit(stats, CombatHitClass.Bullet, rule);
                CombatHitScoring.Credit(stats, CombatHitClass.Debuff, rule);

                Assert.AreEqual(1, stats.BulletHitsLanded);
                Assert.AreEqual(1, stats.DebuffHitsLanded);
                Assert.AreEqual(0, stats.MissileHitsLanded);
                Assert.AreEqual(1, stats.CombatPoints, "Dog Fight pays 1 for a bullet and 0 for a debuff");
            }
            finally { UnityEngine.Object.DestroyImmediate(rule); }
        }

        #endregion

        #region A bullet has to be able to REACH a pilot

        // The report that started this: "my gun didn't seem to score any hits". Every effect was
        // wired; the rounds were tunnelling. PhysX samples a trigger once per FIXED step (0.04s
        // here), a Sparrow round travels 15u in one at its base speed, and an enemy hull presents
        // roughly a 6u window - so most otherwise-perfect shots passed through with PhysX never
        // sampling inside them. Prisms were already swept; vessels were not.
        [Test]
        public void BothSparrowGunRoundsSweepForVessels()
        {
            foreach (var prefab in new[] { BulletPrefab, TurretPrismPrefab })
            {
                string yaml = Read(prefab);
                Assert.AreEqual(1f, Field(yaml, "sweptVesselDetection"),
                                $"{Path.GetFileName(prefab)} relies on the PhysX trigger for " +
                                "vessel contact and will tunnel through pilots");
                Assert.AreEqual(1f, Field(yaml, "sweptPrismDetection"),
                                $"{Path.GetFileName(prefab)} stopped sweeping for prisms");
            }
        }

        #endregion

        #region The missile charge economy and its gauge

        [Test]
        public void FiftyHostilePrismsBuyOneRocket()
        {
            float perPrism = Field(Read(SparrowVessel), "ammoPerPrism");
            float cost = Field(Read(SkyBurstAction), "ammoCost");

            Assert.Greater(perPrism, 0f, "0 disables the rearm entirely");
            Assert.AreEqual(50f, cost / perPrism, 0.5f,
                            "the ask was to DOUBLE the prisms per rocket (was 25)");
        }

        // The gauge is what the pilot reads that number off. It is bound on the CHARGE card
        // because Charge is the element that upgrades the skyburst, and the row is ordered
        // charge/mass/space/time - so the missile's card is the first one.
        [Test]
        public void TheChargeCardCarriesTheMissileGauge()
        {
            string hud = Read(SparrowHud);
            var m = Regex.Match(hud, @"^  abilityIcons:\n  - element: 1\n(?:    \w+: [^\n]*\n)+",
                                RegexOptions.Multiline);
            Assert.IsTrue(m.Success, "the Charge ability binding is not authored");
            StringAssert.Contains("gauge: {fileID:", m.Value,
                                  "the Charge card has no gauge - the missile charge has " +
                                  "nowhere to draw");
            Assert.IsFalse(m.Value.Contains("gauge: {fileID: 0}"), "the Charge gauge is unassigned");
        }

        // The tank holds TWO rockets, so "how full is the tank" and "how close is the next
        // rocket" are different questions; the icon ladder answers the first and the gauge the
        // second. Cost 0.5 of a full tank.
        [Test]
        public void ChargeToNextShotFillsResetsAndFinishesFull()
        {
            const float cost = 0.5f;

            Assert.AreEqual(0f, FireGunActionExecutor.ChargeToNextShot(0f, cost), 1e-5f);
            Assert.AreEqual(0.5f, FireGunActionExecutor.ChargeToNextShot(0.25f, cost), 1e-5f);

            // One rocket earned: the bar has just run off the top and started again.
            Assert.AreEqual(0f, FireGunActionExecutor.ChargeToNextShot(0.5f, cost), 1e-5f);
            Assert.AreEqual(0.5f, FireGunActionExecutor.ChargeToNextShot(0.75f, cost), 1e-5f);

            // A FULL rack reads FULL, not empty: frac(2.0) is 0, and a gauge that empties the
            // moment the bay fills says the opposite of the truth.
            Assert.AreEqual(1f, FireGunActionExecutor.ChargeToNextShot(1f, cost), 1e-5f);
        }

        [Test]
        public void ChargeToNextShotDegradesToTheTankWhenTheCostIsUnknown()
        {
            // No weapon asset to ask (before the first shot on a prefab that wires none), or a
            // free shot: the gauge is then just the tank, which is the honest fallback.
            Assert.AreEqual(0.4f, FireGunActionExecutor.ChargeToNextShot(0.4f, 0f), 1e-5f);
        }

        #endregion

        /// <summary>Minimal <see cref="IRoundStats"/> - only the four combat members matter
        /// here; the rest exist because the interface demands them.</summary>
        class FakeRoundStats : IRoundStats
        {
#pragma warning disable CS0067
            public event Action<IRoundStats> OnAnyStatChanged;
            public event Action OnScoreChanged;
            public event Action<IRoundStats> OnBlocksCreatedChanged;
            public event Action<IRoundStats> OnBlocksDestroyedChanged;
            public event Action<IRoundStats> OnBlocksRestoredChanged;
            public event Action<IRoundStats> OnPrismsStolenChanged;
            public event Action<IRoundStats> OnPrismsRemainingChanged;
            public event Action<IRoundStats> OnFriendlyPrismsDestroyedChanged;
            public event Action<IRoundStats> OnHostilePrismsDestroyedChanged;
            public event Action<IRoundStats> OnVolumeCreatedChanged;
            public event Action<IRoundStats> OnTotalVolumeDestroyedChanged;
            public event Action<IRoundStats> OnFriendlyVolumeDestroyedChanged;
            public event Action<IRoundStats> OnHostileVolumeDestroyedChanged;
            public event Action<IRoundStats> OnVolumeRestoredChanged;
            public event Action<IRoundStats> OnVolumeStolenChanged;
            public event Action<IRoundStats> OnVolumeRemainingChanged;
            public event Action<IRoundStats> OnCrystalsCollectedChanged;
            public event Action<IRoundStats> OnOmniCrystalsCollectedChanged;
            public event Action<IRoundStats> OnElementalCrystalsCollectedChanged;
            public event Action<IRoundStats> OnChargeCrystalValueChanged;
            public event Action<IRoundStats> OnMassCrystalValueChanged;
            public event Action<IRoundStats> OnSpaceCrystalValueChanged;
            public event Action<IRoundStats> OnTimeCrystalValueChanged;
            public event Action<IRoundStats> OnSkimmerShipCollisionsChanged;
            public event Action<IRoundStats> OnJoustCollisionChanged;
            public event Action<IRoundStats> OnGoalsScoredChanged;
            public event Action<IRoundStats> OnLifeformsKilledChanged;
            public event Action<IRoundStats> OnBulletHitsLandedChanged;
            public event Action<IRoundStats> OnMissileHitsLandedChanged;
            public event Action<IRoundStats> OnDebuffHitsLandedChanged;
            public event Action<IRoundStats> OnCombatPointsChanged;
            public event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
            public event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
            public event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;
#pragma warning restore CS0067

            public string Name { get; set; }
            public Domains Domain { get; set; }
            public float Score { get; set; }
            public int BlocksCreated { get; set; }
            public int BlocksDestroyed { get; set; }
            public int BlocksRestored { get; set; }
            public int PrismStolen { get; set; }
            public int PrismsRemaining { get; set; }
            public int FriendlyPrismsDestroyed { get; set; }
            public int HostilePrismsDestroyed { get; set; }
            public float VolumeCreated { get; set; }
            public float TotalVolumeDestroyed { get; set; }
            public float VolumeRestored { get; set; }
            public float VolumeStolen { get; set; }
            public float VolumeRemaining { get; set; }
            public float FriendlyVolumeDestroyed { get; set; }
            public float HostileVolumeDestroyed { get; set; }
            public int CrystalsCollected { get; set; }
            public int OmniCrystalsCollected { get; set; }
            public int ElementalCrystalsCollected { get; set; }
            public float ChargeCrystalValue { get; set; }
            public float MassCrystalValue { get; set; }
            public float SpaceCrystalValue { get; set; }
            public float TimeCrystalValue { get; set; }
            public int SkimmerShipCollisions { get; set; }
            public int JoustCollisions { get; set; }
            public int GoalsScored { get; set; }
            public int LifeformsKilled { get; set; }
            public int BulletHitsLanded { get; set; }
            public int MissileHitsLanded { get; set; }
            public int DebuffHitsLanded { get; set; }
            public int CombatPoints { get; set; }
            public float FullSpeedStraightAbilityActiveTime { get; set; }
            public float RightStickAbilityActiveTime { get; set; }
            public float LeftStickAbilityActiveTime { get; set; }
            public float FlipAbilityActiveTime { get; set; }
            public float Button1AbilityActiveTime { get; set; }
            public float Button2AbilityActiveTime { get; set; }
            public float Button3AbilityActiveTime { get; set; }
        }
    }
}
#endif

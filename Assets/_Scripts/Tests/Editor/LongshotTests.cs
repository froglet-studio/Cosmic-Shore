#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Longshot (the Serpent's Space game, LONGSHOT.md): the field's ladder and geometry, the sniper's cone restated, the
    /// scoring rule and the AI ladders. The field is laid on every peer from the intensity alone; the score is hostile
    /// prisms broken (PrismsDestroyed), decided by the clock.
    /// </summary>
    [TestFixture]
    public class LongshotTests
    {
        readonly List<Object> _owned = new();
        T Track<T>(T o) where T : Object { _owned.Add(o); return o; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _owned) if (o) Object.DestroyImmediate(o);
            _owned.Clear();
        }

        LongshotScoringRuleSO MakeRule()
        {
            var rule = Track(ScriptableObject.CreateInstance<LongshotScoringRuleSO>());
            typeof(ScoringRuleSO).GetField("metric", System.Reflection.BindingFlags.Instance |
                                                     System.Reflection.BindingFlags.NonPublic)
                                 .SetValue(rule, ScoringMetric.PrismsDestroyed);
            typeof(ScoringRuleSO).GetField("golfRules", System.Reflection.BindingFlags.Instance |
                                                        System.Reflection.BindingFlags.NonPublic)
                                 .SetValue(rule, false);
            return rule;
        }

        GameDataSO MakeMatch(int domainCount, params (string name, Domains domain, int broken, float volume)[] pilots)
        {
            var gameData = Track(ScriptableObject.CreateInstance<GameDataSO>());
            gameData.RequestedDomainCount = domainCount;
            foreach (var (name, domain, broken, volume) in pilots)
                gameData.RoundStatsList.Add(new FakeStats
                    { Name = name, Domain = domain, HostilePrismsDestroyed = broken, HostileVolumeDestroyed = volume });
            return gameData;
        }

        [Test]
        public void Rule_NeverEndsTheRoundEarly()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 999999, 9f), ("B", Domains.Ruby, 0, 0f));
            Assert.IsFalse(rule.IsObjectiveReached(match, out var winner), "a live stock is decided by the clock");
            Assert.AreEqual(Domains.Blue, winner);
        }

        [Test]
        public void Rule_MostPrismsBroken_Wins_SummedByDomain()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 40, 1f), ("B", Domains.Jade, 40, 1f), ("C", Domains.Ruby, 70, 1f));
            Assert.AreEqual(Domains.Jade, rule.ResolveWinner(match), "two pilots' 80 beats one pilot's 70");
        }

        [Test]
        public void Rule_Results_ReadTheAuthoredTemplates()
        {
            var rule = MakeRule();
            var match = MakeMatch(2, ("A", Domains.Jade, 90, 1944.4f), ("B", Domains.Ruby, 30, 648f));
            rule.AssignScores(match, Domains.Jade, 90f);
            Assert.AreEqual(90f, match.RoundStatsList[0].Score, 1e-3f);
            var rows = rule.BuildResults(match);
            Assert.AreEqual("A", rows[0].Name);
            Assert.AreEqual("90 Prisms", rows[0].ScoreText);
            Assert.AreEqual("1944 Volume", rows[0].Secondary);
            Assert.AreEqual("648 Volume", rows[1].Secondary);
        }

        [Test]
        public void Field_GrowsOrBendsEveryRung_UnderTheColliderBudget()
        {
            int lastPrisms = 0, lastRuns = 0;
            for (int i = 1; i <= 4; i++)
            {
                var rung = LongshotField.ForIntensity(i);
                int prisms = LongshotField.TotalPrisms(rung);
                int runs = LongshotField.TotalSegments(rung);
                Assert.IsTrue(prisms > lastPrisms || runs > lastRuns, $"I{i} lays more field or more runs than the rung below");
                Assert.GreaterOrEqual(prisms, lastPrisms);
                Assert.Less(prisms, 3500, $"I{i} stays under the collider budget");
                lastPrisms = prisms;
                lastRuns = runs;
            }
        }

        [Test]
        public void Field_LaysTheCountedPrisms_InStraightRuns_BetweenNucleusAndMembrane()
        {
            var pieces = new List<LongshotField.Piece>();
            var segments = new List<LongshotField.Segment>();
            for (int i = 1; i <= 4; i++)
            {
                var rung = LongshotField.ForIntensity(i);
                LongshotField.Lay(rung, 392f, 1200f, pieces, segments);
                Assert.AreEqual(LongshotField.TotalPrisms(rung), pieces.Count, $"I{i}");
                Assert.AreEqual(LongshotField.TotalSegments(rung), segments.Count, $"I{i}");
                foreach (var p in pieces)
                {
                    Assert.Greater(p.Position.magnitude, 392f + 50f, $"I{i}: a lance prism too close to the nucleus");
                    Assert.Less(p.Position.magnitude, 1200f - 50f, $"I{i}: a lance prism too close to the membrane");
                    // Every prism lies ON its run's line, so a round down the line takes all of it.
                    var seg = segments[p.Segment];
                    Vector3 dir = (seg.End - seg.Start).normalized;
                    Vector3 rel = p.Position - seg.Start;
                    Assert.Less((rel - dir * Vector3.Dot(rel, dir)).magnitude, 1e-2f, $"I{i}: a prism off its run's line");
                }
            }
        }

        [Test]
        public void Cone_DownTheRun_TakesItAll_AcrossIt_TakesAlmostNothing()
        {
            var pieces = new List<LongshotField.Piece>();
            var segments = new List<LongshotField.Segment>();
            var rung = LongshotField.ForIntensity(1);
            LongshotField.Lay(rung, 392f, 1200f, pieces, segments);
            var seg = segments[0];
            Vector3 dir = (seg.End - seg.Start).normalized;
            Vector3 apex = seg.Start - dir * 260f;
            int along = 0, across = 0;
            Vector3 side = Vector3.Cross(dir, seg.Start.normalized).normalized;
            Vector3 mid = (seg.Start + seg.End) * 0.5f;
            foreach (var p in pieces)
            {
                if (p.Segment != 0) continue;
                if (LongshotField.InCone(p.Position, apex, dir, 3000f, 1.5f, 6f)) along++;
                if (LongshotField.InCone(p.Position, mid - side * 260f, side, 3000f, 1.5f, 6f)) across++;
            }
            Assert.AreEqual(seg.Count, along, "a round fired down the run breaks every prism on it");
            Assert.LessOrEqual(across, 2, "a round fired across the run breaks a prism or two");
        }

        [Test]
        public void Field_TheSameFieldIsLaidTwice_AndBentLancesTurnAtTheKnee()
        {
            var a = new List<LongshotField.Piece>();
            var b = new List<LongshotField.Piece>();
            var sa = new List<LongshotField.Segment>();
            var sb = new List<LongshotField.Segment>();
            var rung = LongshotField.ForIntensity(3);
            LongshotField.Lay(rung, 392f, 1200f, a, sa);
            LongshotField.Lay(rung, 392f, 1200f, b, sb);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].Position, b[i].Position, "deterministic");
            Vector3 d0 = (sa[0].End - sa[0].Start).normalized;
            Vector3 d1 = (sa[1].End - sa[1].Start).normalized;
            Assert.AreEqual(2f * LongshotField.KneeDegrees, Vector3.Angle(d0, d1), 0.5f, "the two runs of a bent lance");
        }

        [Test]
        public void Settings_AiLadders_ReadByDifficulty()
        {
            var settings = Track(ScriptableObject.CreateInstance<LongshotSettingsSO>());
            Assert.AreEqual(1f, LongshotSettingsSO.Rung(settings.aiRivalTrailDeficitFraction, (int)AIDifficulty.Easy),
                "Easy never goes for a rival's trail");
            Assert.Greater(LongshotSettingsSO.Rung(settings.aiFireFraction, (int)AIDifficulty.Legendary),
                           LongshotSettingsSO.Rung(settings.aiFireFraction, (int)AIDifficulty.Easy),
                           "a harder rifleman waits to be lined up");
            Assert.Less(LongshotSettingsSO.Rung(settings.aiScopeSettleSeconds, (int)AIDifficulty.Legendary),
                        LongshotSettingsSO.Rung(settings.aiScopeSettleSeconds, (int)AIDifficulty.Easy));
            Assert.AreEqual(0f, LongshotSettingsSO.Rung(null, 2));
        }

        private sealed class FakeStats : IRoundStats
        {
        // The interface, stubbed (copied from RegattaTeamPlayTests). Only Name, Domain,
        // HostileVolumeDestroyed and FusesBeaten are read by the rules under test; the rest
        // exist so the stub compiles.
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
        public int StrikeHitsLanded { get; set; }
        public int CombatPoints { get; set; }
        public int SwitchesThreaded { get; set; }
        public int FusesBeaten { get; set; }
        public int Lives { get; set; }
        public bool IsEliminated { get; set; }
        public float FullSpeedStraightAbilityActiveTime { get; set; }
        public float RightStickAbilityActiveTime { get; set; }
        public float LeftStickAbilityActiveTime { get; set; }
        public float FlipAbilityActiveTime { get; set; }
        public float Button1AbilityActiveTime { get; set; }
        public float Button2AbilityActiveTime { get; set; }
        public float Button3AbilityActiveTime { get; set; }

#pragma warning disable 67   // never raised: the driver polls, it does not subscribe
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
        public event Action<IRoundStats> OnStrikeHitsLandedChanged;
        public event Action<IRoundStats> OnCombatPointsChanged;
        public event Action<IRoundStats> OnSwitchesThreadedChanged;
        public event Action<IRoundStats> OnFusesBeatenChanged;
        public event Action<IRoundStats> OnLivesChanged;
        public event Action<IRoundStats> OnEliminatedChanged;
        public event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;
#pragma warning restore 67
        }
    }
}
#endif
